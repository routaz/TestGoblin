using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Routaz.TestGoblin.Runtime.Config;
using Routaz.TestGoblin.Runtime.Driver;
using Routaz.TestGoblin.Runtime.Scenarios;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Routaz.TestGoblin.Tests.PlayMode
{
    /// <summary>
    /// Turns every scenario file into its own NUnit test case.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the join between authored data and the test runner. Because the file list is a
    /// <see cref="ValueSourceAttribute"/>, each scenario appears in Test Runner and in CI under
    /// its own name, passing or failing independently — with no generated C#, no domain reload
    /// and no compile step between saving a scenario and running it.
    /// </para>
    /// <para>
    /// NUnit enumerates a value source at <em>discovery</em> time, so a brand-new scenario file
    /// needs the editor's test list to refresh before it appears. In batch mode this is a
    /// non-issue: the process starts, discovers, and runs.
    /// </para>
    /// <para>
    /// Play mode, not edit mode: the driver's waits need frames pumping to complete.
    /// </para>
    /// </remarks>
    [Category("Scenarios")]
    public class ScenarioRunnerTests
    {
        private const float Timeout = 10f;
        private const int PollIntervalMs = 100;

        /// <summary>Points at a config file when a project needs a non-default driver.</summary>
        public const string ConfigPathEnvironmentVariable = "TESTGOBLIN_CONFIG_PATH";

        /// <summary>
        /// The scenario file names, which double as the generated test case names.
        /// </summary>
        /// <remarks>
        /// File names rather than full paths, so a failing case in CI reads as
        /// <c>Scenario("ftue.scenario.json")</c> instead of a machine-specific absolute path.
        /// </remarks>
        public static IEnumerable<string> ScenarioFiles()
        {
            return ScenarioLoader.ListFiles(ScenariosDirectory()).Select(Path.GetFileName);
        }

        private bool _loadedAScene;

        /// <summary>
        /// Tears down whatever scene the scenario loaded.
        /// </summary>
        /// <remarks>
        /// A loaded scene outlives the test that loaded it, so without this a scenario leaves
        /// its game standing for everything that runs afterwards — an open menu, a wall, a
        /// camera. The symptom is other tests failing for reasons that have nothing to do with
        /// them, which is expensive to diagnose and trivial to prevent.
        /// </remarks>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!_loadedAScene)
            {
                yield break;
            }

            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                UnityEngine.Object.Destroy(root);
            }

            _loadedAScene = false;

            // Destruction is deferred to end of frame, so yield before anything else looks.
            yield return null;
        }

        [UnityTest]
        public IEnumerator Scenario([ValueSource(nameof(ScenarioFiles))] string fileName)
        {
            Scenario scenario = ScenarioLoader.Load(Path.Combine(ScenariosDirectory(), fileName));

            if (!string.IsNullOrEmpty(scenario.Scene))
            {
                _loadedAScene = true;
                SceneManager.LoadScene(scenario.Scene);

                // One frame to commit the load, one for Awake/Start to establish the
                // starting state the scenario's first step assumes.
                yield return null;
                yield return null;
            }

            var executor = new ScenarioExecutor(
                KeywordRegistry.FromLoadedAssemblies(),
                GameDriverFactory.CreateLayered(ResolveConfig(), BackendsFor(scenario)));

            Task run = executor.Execute(scenario);

            // Yielding keeps the main thread pumping, which is what lets the driver's
            // awaited continuations return to it.
            while (!run.IsCompleted)
            {
                yield return null;
            }

            if (run.IsFaulted)
            {
                // Unwrap so the ScenarioStepException — which names the file and step —
                // is what gets reported, not an AggregateException wrapping it.
                throw run.Exception?.InnerException ?? run.Exception;
            }
        }

        private static string ScenariosDirectory()
        {
            return ScenarioLoader.ResolveDirectory(Path.GetDirectoryName(Application.dataPath));
        }

        /// <summary>
        /// The input backends this scenario asks for, topmost first, or empty to use the
        /// run's configured one.
        /// </summary>
        /// <remarks>
        /// A comma-separated value describes a stack — "UiToolkit,Physics3D" for a HUD over a
        /// 3D world — which becomes a composite driver that refuses a click when an upper
        /// layer covers the target.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The scenario names an unknown backend.</exception>
        private static IReadOnlyList<InputBackend> BackendsFor(Scenario scenario)
        {
            if (string.IsNullOrWhiteSpace(scenario.InputBackend))
            {
                return Array.Empty<InputBackend>();
            }

            var backends = new List<InputBackend>();

            foreach (string name in scenario.InputBackend.Split(','))
            {
                string trimmed = name.Trim();

                if (trimmed.Length == 0)
                {
                    continue;
                }

                if (!Enum.TryParse(trimmed, ignoreCase: true, out InputBackend backend))
                {
                    throw new InvalidOperationException(
                        $"Scenario '{scenario.Name}' asks for input backend '{trimmed}', which is not " +
                        $"one of: {string.Join(", ", Enum.GetNames(typeof(InputBackend)))}. " +
                        $"[{scenario.SourcePath}]");
                }

                backends.Add(backend);
            }

            return backends;
        }

        /// <summary>
        /// The config this run should use: the file named by <c>TESTGOBLIN_CONFIG_PATH</c>,
        /// or an in-process uGUI default.
        /// </summary>
        /// <remarks>
        /// The fallback matters more than the file. A consumer who has just imported the
        /// package should be able to run a scenario without first authoring configuration,
        /// so the common case needs no setup — while a project that does care (a physics
        /// game, a device run) points the environment variable at a config and gets a
        /// different driver with no code change. That is the whole claim behind
        /// "switching targets changes config, never code".
        /// </remarks>
        private static TestRunConfig ResolveConfig()
        {
            string path = Environment.GetEnvironmentVariable(ConfigPathEnvironmentVariable);

            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                return ConfigLoader.Load(path);
            }

            return new TestRunConfig
            {
                ExecutionTarget = ExecutionTarget.Editor,
                InputBackend = InputBackend.UGui,
                DefaultTimeout = Timeout,
                DefaultPollInterval = PollIntervalMs
            };
        }
    }
}
