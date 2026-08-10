using TestGoblin.Server;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

const string ServerUrl = "http://127.0.0.1:5111";

// This server starts processes on the developer's machine. Binding to loopback
// only means nothing on the network can reach it, regardless of firewall state.
builder.WebHost.UseUrls(ServerUrl);

UnityOptions unityOptions =
    builder.Configuration.GetSection("Unity").Get<UnityOptions>() ?? new UnityOptions();

// Anchor a relative runs directory to the project root, so artifacts land in the
// same place no matter which directory the server was launched from.
unityOptions.RunsDirectory =
    Path.GetFullPath(unityOptions.RunsDirectory, builder.Environment.ContentRootPath);

ServerOptions serverOptions =
    builder.Configuration.GetSection("Server").Get<ServerOptions>() ?? new ServerOptions();

ScenarioOptions scenarioOptions =
    builder.Configuration.GetSection("Scenarios").Get<ScenarioOptions>() ?? new ScenarioOptions();

// Same anchoring as the runs directory: relative paths resolve against the project root
// so behaviour does not depend on the launch directory.
scenarioOptions.Directory =
    Path.GetFullPath(scenarioOptions.Directory, builder.Environment.ContentRootPath);
scenarioOptions.CatalogPath =
    Path.GetFullPath(scenarioOptions.CatalogPath, builder.Environment.ContentRootPath);

builder.Services.AddSingleton(unityOptions);
builder.Services.AddSingleton(serverOptions);
builder.Services.AddSingleton(scenarioOptions);
builder.Services.AddSingleton<ScenarioStore>();
builder.Services.AddSingleton<UnityRunner>();

// Registered twice on purpose, resolving to ONE instance: the heartbeat endpoint
// needs the same object the background loop is reading. AddHostedService<T>() alone
// would construct a second instance, and Touch() would update the wrong one.
builder.Services.AddSingleton<IdleShutdownService>();
builder.Services.AddHostedService(services => services.GetRequiredService<IdleShutdownService>());

WebApplication app = builder.Build();

// Serves wwwroot/index.html at "/".
app.UseDefaultFiles();
app.UseStaticFiles();

// Starts a run and returns immediately — the browser polls GET /api/runs/{id}.
app.MapPost("/api/runs", (UnityRunner runner, string? platform) =>
{
    string requested = platform ?? "EditMode";

    // Validated here rather than passed through: Unity would accept an unknown value,
    // spend a minute starting up, and then fail in a way that looks like a broken run
    // instead of a bad request.
    if (!string.Equals(requested, "EditMode", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(requested, "PlayMode", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { error = $"Unknown test platform '{requested}'. Use EditMode or PlayMode." });
    }

    TestRun run = runner.Start(requested);
    return Results.Accepted($"/api/runs/{run.Id}", ToResponse(run));
});

app.MapGet("/api/runs", (UnityRunner runner) =>
{
    return Results.Ok(runner.List().Select(ToResponse));
});

app.MapGet("/api/runs/{id}", (UnityRunner runner, string id) =>
{
    TestRun? run = runner.Get(id);
    return run is null ? Results.NotFound() : Results.Ok(ToResponse(run));
});

// The vocabulary the scenario builder offers. Generated from [ScenarioAction] attributes
// by the Unity-side exporter, so C# stays the single source of truth.
app.MapGet("/api/keywords", (ScenarioStore store) =>
{
    string? catalog = store.ReadCatalog();

    if (catalog is null)
    {
        // An absent catalog is a setup step, not a crash — say exactly how to fix it.
        return Results.NotFound(new
        {
            error = "No keyword catalog yet. In Unity run TestGoblin > Export Keyword Catalog, "
                  + "or run the exporter in batch mode."
        });
    }

    return Results.Content(catalog, "application/json");
});

app.MapGet("/api/scenarios", (ScenarioStore store) =>
{
    return Results.Ok(new { directory = store.Directory, files = store.List() });
});

app.MapGet("/api/scenarios/{fileName}", (ScenarioStore store, string fileName) =>
{
    try
    {
        return Results.Content(store.Read(fileName), "application/json");
    }
    catch (FileNotFoundException)
    {
        return Results.NotFound(new { error = $"No scenario named '{fileName}'." });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapPut("/api/scenarios/{fileName}", async (ScenarioStore store, string fileName, HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    string body = await reader.ReadToEndAsync();

    try
    {
        store.Write(fileName, body);
        return Results.Ok(new { saved = fileName });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

app.MapDelete("/api/scenarios/{fileName}", (ScenarioStore store, string fileName) =>
{
    try
    {
        store.Delete(fileName);
        return Results.Ok(new { deleted = fileName });
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new { error = exception.Message });
    }
});

// The UI holds this connection open for as long as its tab is open. It is the
// liveness signal: when it drops, the server knows the tab is gone and exits.
// Server-sent events rather than a client timer, because browsers throttle timers
// in background tabs and would make an open-but-hidden tab look closed.
app.MapGet("/api/live", async (HttpContext context, IdleShutdownService idle) =>
{
    context.Response.Headers.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";

    idle.ClientConnected();

    try
    {
        // Tell the browser to reconnect after 1s instead of EventSource's 3s default.
        // The default is exactly IdleGraceSeconds, so a transient drop would race the
        // shutdown — and losing that race kills the server under an open tab.
        await context.Response.WriteAsync("retry: 1000\n\n", context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));

        while (await timer.WaitForNextTickAsync(context.RequestAborted))
        {
            // An SSE comment line: EventSource ignores it, but writing is how we
            // notice a client that vanished without a clean TCP close.
            await context.Response.WriteAsync(": ping\n\n", context.RequestAborted);
            await context.Response.Body.FlushAsync(context.RequestAborted);
        }
    }
    catch (OperationCanceledException)
    {
        // The tab closed. This is the expected way out of this loop.
    }
    catch (IOException)
    {
        // The connection dropped mid-write — same meaning, different symptom.
    }
    finally
    {
        idle.ClientDisconnected();
    }
});

if (serverOptions.OpenBrowserOnStart)
{
    // Registered rather than called inline: at this point the server is not
    // listening yet, so opening the browser now would race the first request.
    app.Lifetime.ApplicationStarted.Register(() => OpenBrowser(ServerUrl));
}

app.Run();

/// <summary>
/// Opens the UI in the default browser. UseShellExecute is required: it hands the
/// URL to the OS handler instead of trying to execute it as a program.
/// </summary>
static void OpenBrowser(string url)
{
    try
    {
        using var _ = System.Diagnostics.Process.Start(
            new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
    }
    catch (Exception exception)
    {
        // Never fatal — the URL is printed in the console either way.
        Console.WriteLine($"Could not open a browser ({exception.Message}). Open {url} yourself.");
    }
}

/// <summary>
/// Projects a run into the shape the UI consumes. Kept explicit rather than
/// serialising <see cref="TestRun"/> directly so the wire contract is visible
/// in one place and cannot drift silently when the internal model changes.
/// </summary>
static object ToResponse(TestRun run)
{
    return new
    {
        id = run.Id,
        state = run.State.ToString(),
        testPlatform = run.TestPlatform,
        startedUtc = run.StartedUtc,
        finishedUtc = run.FinishedUtc,
        exitCode = run.ExitCode,
        error = run.Error,
        logPath = Path.GetFullPath(run.LogPath),
        summary = run.Results is null
            ? null
            : new
            {
                result = run.Results.Result,
                total = run.Results.Total,
                passed = run.Results.Passed,
                failed = run.Results.Failed,
                skipped = run.Results.Skipped,
                inconclusive = run.Results.Inconclusive,
                durationSeconds = run.Results.DurationSeconds
            },
        cases = run.Results?.Cases.Select(testCase => new
        {
            fullName = testCase.FullName,
            className = testCase.ClassName,
            name = testCase.Name,
            result = testCase.Result,
            durationSeconds = testCase.DurationSeconds,
            failureMessage = testCase.FailureMessage,
            stackTrace = testCase.StackTrace
        })
    };
}
