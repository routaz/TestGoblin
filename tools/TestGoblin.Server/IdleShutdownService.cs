namespace TestGoblin.Server;

/// <summary>Behaviour of the server process itself. Bound from appsettings.json.</summary>
public sealed class ServerOptions
{
    /// <summary>
    /// Exit once no browser is connected. On by default: this is a local tool and
    /// should not outlive the tab that opened it. Turn off for CI, or when driving
    /// the API without a browser attached.
    /// </summary>
    public bool ExitWhenIdle { get; set; } = true;

    /// <summary>
    /// How long to wait after the last tab disconnects before exiting.
    /// </summary>
    /// <remarks>
    /// Must comfortably exceed the browser's SSE reconnect delay, or a transient drop
    /// races the shutdown and the server dies under a tab that is still open. The
    /// /api/live endpoint asks for a 1s reconnect, so this leaves a wide margin.
    /// </remarks>
    public int IdleGraceSeconds { get; set; } = 8;

    /// <summary>
    /// How long to wait for the first browser to connect before giving up. Covers
    /// a cold browser start, which can take considerably longer than a reload.
    /// </summary>
    public int StartupTimeoutSeconds { get; set; } = 30;

    /// <summary>Open the UI in the default browser once the server is listening.</summary>
    public bool OpenBrowserOnStart { get; set; } = true;
}

/// <summary>
/// Shuts the server down once no browser tab is connected, so nothing is left
/// running in the background after you close the UI.
/// </summary>
/// <remarks>
/// Liveness is a held connection rather than a heartbeat the page sends on a timer.
/// Browsers throttle timers in hidden tabs — Chrome eventually down to about one
/// wake per minute — so a timer-based heartbeat makes a backgrounded-but-open tab
/// look closed. A held connection is driven by the server instead, so throttling
/// cannot affect it, and closing the tab drops the socket immediately rather than
/// having to be inferred from silence.
/// </remarks>
public sealed class IdleShutdownService : BackgroundService
{
    private readonly ServerOptions _options;
    private readonly UnityRunner _runner;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<IdleShutdownService> _logger;

    // Mutated from request threads, read by the timer loop.
    private int _connectedClients;
    private long _idleSinceTicks = DateTime.UtcNow.Ticks;
    private volatile bool _hasEverConnected;

    public IdleShutdownService(
        ServerOptions options,
        UnityRunner runner,
        IHostApplicationLifetime lifetime,
        ILogger<IdleShutdownService> logger)
    {
        _options = options;
        _runner = runner;
        _lifetime = lifetime;
        _logger = logger;
    }

    /// <summary>Records that a UI connection opened. Called by the /api/live endpoint.</summary>
    public void ClientConnected()
    {
        int count = Interlocked.Increment(ref _connectedClients);
        _hasEverConnected = true;
        _logger.LogDebug("UI connected ({Count} open).", count);
    }

    /// <summary>Records that a UI connection closed.</summary>
    public void ClientDisconnected()
    {
        int count = Interlocked.Decrement(ref _connectedClients);

        if (count <= 0)
        {
            // Start the grace countdown from the moment the last tab went away.
            Interlocked.Exchange(ref _idleSinceTicks, DateTime.UtcNow.Ticks);
        }

        _logger.LogDebug("UI disconnected ({Count} open).", count);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.ExitWhenIdle)
        {
            _logger.LogInformation("Idle shutdown disabled; server will run until stopped.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                if (Volatile.Read(ref _connectedClients) > 0)
                {
                    continue;
                }

                // An in-flight Unity run counts as activity, and keeps resetting the
                // countdown. Closing the tab mid-run therefore lets the run finish
                // rather than orphaning Unity.exe or discarding results.
                if (_runner.HasActiveRun())
                {
                    Interlocked.Exchange(ref _idleSinceTicks, DateTime.UtcNow.Ticks);
                    continue;
                }

                var idleSince = new DateTime(
                    Interlocked.Read(ref _idleSinceTicks), DateTimeKind.Utc);

                TimeSpan idleFor = DateTime.UtcNow - idleSince;

                // Before the first connection we are waiting for a browser to start,
                // which is much slower than the reconnect after a reload.
                TimeSpan deadline = _hasEverConnected
                    ? TimeSpan.FromSeconds(_options.IdleGraceSeconds)
                    : TimeSpan.FromSeconds(_options.StartupTimeoutSeconds);

                if (idleFor >= deadline)
                {
                    _logger.LogInformation(
                        _hasEverConnected
                            ? "UI closed — shutting down."
                            : "No UI connected within {Seconds:F0}s — shutting down.",
                        idleFor.TotalSeconds);

                    _lifetime.StopApplication();
                    return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown path — the host cancelled us first.
        }
    }
}
