using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace TestGoblin.Server;

/// <summary>Where to find Unity and the project under test. Bound from appsettings.json.</summary>
public sealed class UnityOptions
{
    /// <summary>Absolute path to Unity.exe. Must match the project's editor version.</summary>
    public string EditorPath { get; set; } = string.Empty;

    /// <summary>Absolute path to the Unity project folder (the one containing Assets/).</summary>
    public string ProjectPath { get; set; } = string.Empty;

    /// <summary>Directory for per-run artifacts (results XML + Unity log). Git-ignored.</summary>
    public string RunsDirectory { get; set; } = ".runs";
}

/// <summary>Lifecycle state of a single test run.</summary>
public enum RunState
{
    /// <summary>Unity is still executing.</summary>
    Running,

    /// <summary>Unity exited and results were parsed successfully.</summary>
    Completed,

    /// <summary>Unity could not be started, or exited without usable results.</summary>
    Failed
}

/// <summary>
/// A single invocation of Unity's test runner, tracked from launch to parsed results.
/// </summary>
public sealed class TestRun
{
    /// <summary>Opaque id the browser polls with.</summary>
    public required string Id { get; init; }

    /// <summary>Which Unity test platform was requested (EditMode or PlayMode).</summary>
    public required string TestPlatform { get; init; }

    /// <summary>
    /// The single scenario this run was narrowed to, or null when the whole suite ran.
    /// </summary>
    public string? ScenarioFile { get; init; }

    /// <summary>Current lifecycle state.</summary>
    public RunState State { get; set; } = RunState.Running;

    /// <summary>When the Unity process was launched.</summary>
    public DateTimeOffset StartedUtc { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>When Unity exited; null while still running.</summary>
    public DateTimeOffset? FinishedUtc { get; set; }

    /// <summary>Unity's process exit code; null while still running.</summary>
    public int? ExitCode { get; set; }

    /// <summary>Parsed results; null until the run completes successfully.</summary>
    public TestRunResult? Results { get; set; }

    /// <summary>Human-readable failure reason when <see cref="State"/> is Failed.</summary>
    public string? Error { get; set; }

    /// <summary>Path to this run's Unity log — the first place to look on failure.</summary>
    public required string LogPath { get; init; }

    /// <summary>Path to this run's NUnit3 results XML.</summary>
    public required string ResultsPath { get; init; }
}

/// <summary>
/// Launches Unity in batch mode and tracks runs in memory.
/// </summary>
/// <remarks>
/// Runs are held in a dictionary rather than a database on purpose: this is a
/// local developer tool, and losing run history when the server restarts is
/// an acceptable trade for having no persistence layer to maintain.
/// </remarks>
public sealed class UnityRunner
{
    private readonly UnityOptions _options;
    private readonly ILogger<UnityRunner> _logger;
    private readonly ConcurrentDictionary<string, TestRun> _runs = new();

    public UnityRunner(UnityOptions options, ILogger<UnityRunner> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Fully-qualified name of the fixture that turns scenario files into test cases.
    /// </summary>
    /// <remarks>
    /// Hardcoded because it is the one test the package ships, and its name is part of
    /// TestGoblin's contract rather than something a consuming project chooses.
    /// </remarks>
    private const string ScenarioTestName = "Routaz.TestGoblin.Tests.PlayMode.ScenarioRunnerTests.Scenario";

    /// <summary>
    /// Starts a Unity test run and returns immediately, without waiting for it to finish.
    /// </summary>
    /// <param name="testPlatform">"EditMode" or "PlayMode".</param>
    /// <param name="scenarioFile">
    /// Narrows the run to one scenario file. Null runs everything.
    /// </param>
    /// <returns>A run whose state is Running, or Failed if Unity could not be launched.</returns>
    public TestRun Start(string testPlatform, string? scenarioFile = null)
    {
        string id = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
        string runDirectory = Path.Combine(_options.RunsDirectory, id);
        Directory.CreateDirectory(runDirectory);

        var run = new TestRun
        {
            Id = id,
            TestPlatform = testPlatform,
            ScenarioFile = scenarioFile,
            LogPath = Path.Combine(runDirectory, "unity.log"),
            ResultsPath = Path.Combine(runDirectory, "results.xml")
        };

        _runs[id] = run;

        string? blocker = FindStartupBlocker();
        if (blocker is not null)
        {
            Fail(run, blocker);
            return run;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = _options.EditorPath,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        // ArgumentList quotes each entry for us — safer than building one string,
        // which breaks the moment a path contains a space.
        startInfo.ArgumentList.Add("-runTests");
        startInfo.ArgumentList.Add("-batchmode");
        startInfo.ArgumentList.Add("-projectPath");
        startInfo.ArgumentList.Add(_options.ProjectPath);
        startInfo.ArgumentList.Add("-testPlatform");
        startInfo.ArgumentList.Add(testPlatform);
        startInfo.ArgumentList.Add("-testResults");
        startInfo.ArgumentList.Add(Path.GetFullPath(run.ResultsPath));
        startInfo.ArgumentList.Add("-logFile");
        startInfo.ArgumentList.Add(Path.GetFullPath(run.LogPath));

        if (scenarioFile is not null)
        {
            startInfo.ArgumentList.Add("-testFilter");
            startInfo.ArgumentList.Add(ScenarioFilter(scenarioFile));
        }

        Process process;

        try
        {
            process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Process.Start returned null.");
        }
        catch (Exception exception)
        {
            Fail(run, $"Could not start Unity at '{_options.EditorPath}': {exception.Message}");
            return run;
        }

        _logger.LogInformation("Run {RunId} started: Unity pid {Pid}, platform {Platform}",
            id, process.Id, testPlatform);

        // Fire-and-forget by design: the HTTP response must not wait for Unity.
        // MonitorAsync owns all its own error handling so nothing escapes unobserved.
        _ = MonitorAsync(run, process);

        return run;
    }

    /// <summary>Looks up a run by id.</summary>
    public TestRun? Get(string id)
    {
        return _runs.TryGetValue(id, out TestRun? run) ? run : null;
    }

    /// <summary>
    /// True while any run is still executing. Used to keep the server alive
    /// through a run even if the browser has gone away.
    /// </summary>
    public bool HasActiveRun()
    {
        return _runs.Values.Any(run => run.State == RunState.Running);
    }

    /// <summary>Lists runs, newest first.</summary>
    public IReadOnlyList<TestRun> List()
    {
        return _runs.Values
            .OrderByDescending(run => run.StartedUtc)
            .ToList();
    }

    /// <summary>
    /// Waits for Unity to exit, then parses whatever it produced.
    /// </summary>
    private async Task MonitorAsync(TestRun run, Process process)
    {
        try
        {
            using (process)
            {
                await process.WaitForExitAsync();
                run.ExitCode = process.ExitCode;
            }

            run.FinishedUtc = DateTimeOffset.UtcNow;

            // A non-zero exit code means tests failed, which is a perfectly normal
            // outcome — the results file is still written and still worth parsing.
            // Only a missing or malformed file is a genuine infrastructure failure.
            run.Results = NUnitResultParser.Parse(run.ResultsPath);

            // A filter that matches nothing produces an empty, successful run — which reads
            // as "your scenario passed". Treated as a failure so a filter that silently
            // stopped working can never be mistaken for a green test.
            if (run.ScenarioFile is not null && run.Results.Total == 0)
            {
                Fail(run, $"The run selected no tests for '{run.ScenarioFile}'. Either the file " +
                          "was not on disk when Unity started, or it produced no test case.");
                return;
            }

            run.State = RunState.Completed;

            _logger.LogInformation(
                "Run {RunId} finished: exit {ExitCode}, {Passed}/{Total} passed",
                run.Id, run.ExitCode, run.Results.Passed, run.Results.Total);
        }
        catch (Exception exception)
        {
            Fail(run, $"{exception.Message} (Unity log: {run.LogPath})");
        }
    }

    /// <summary>
    /// The <c>-testFilter</c> value selecting exactly one scenario's generated test case.
    /// </summary>
    /// <remarks>
    /// Unity matches this as a regular expression against each test's full name, and the
    /// generated name — <c>Scenario("my-case.scenario.json")</c> — is full of characters a
    /// regex reads as syntax. The parentheses are the dangerous ones: left unescaped they
    /// become a capture group, and the filter then matches nothing at all.
    /// </remarks>
    private static string ScenarioFilter(string scenarioFile)
    {
        return Regex.Escape($"{ScenarioTestName}(\"{scenarioFile}\")");
    }

    /// <summary>
    /// Checks the conditions that make a run pointless before we spend minutes finding out.
    /// </summary>
    /// <returns>An actionable message, or null if we are clear to start.</returns>
    private string? FindStartupBlocker()
    {
        if (!File.Exists(_options.EditorPath))
        {
            return $"Unity not found at '{_options.EditorPath}'. " +
                   "Set Unity:EditorPath in appsettings.json.";
        }

        if (!Directory.Exists(_options.ProjectPath))
        {
            return $"Unity project not found at '{_options.ProjectPath}'. " +
                   "Set Unity:ProjectPath in appsettings.json.";
        }

        // Unity holds this lock while the editor has the project open, and a
        // batch-mode run against a locked project hangs instead of failing.
        string lockFile = Path.Combine(_options.ProjectPath, "Temp", "UnityLockfile");

        if (File.Exists(lockFile))
        {
            return "The Unity editor currently has this project open. " +
                   "Close the editor before running tests in batch mode.";
        }

        return null;
    }

    private void Fail(TestRun run, string error)
    {
        run.State = RunState.Failed;
        run.Error = error;
        run.FinishedUtc = DateTimeOffset.UtcNow;
        _logger.LogError("Run {RunId} failed: {Error}", run.Id, error);
    }
}
