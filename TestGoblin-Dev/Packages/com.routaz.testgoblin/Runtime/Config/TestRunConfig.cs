using System;

namespace Routaz.TestGoblin.Runtime.Config
{
    /// <summary>
    /// Everything a run needs to know, loaded from JSON with environment overrides.
    /// </summary>
    /// <remarks>
    /// A record rather than a class so callers can derive a variant with <c>with</c> — a
    /// scenario overriding the input backend needs exactly that, and copying every property
    /// by hand would silently drop any field added later.
    /// </remarks>
    public sealed record TestRunConfig
    {
        public ExecutionTarget ExecutionTarget { get; init; }
        public Platform Platform { get; init; }
        public string DeviceId { get; init; } = "";
        public string BuildPath { get; init; } = "";
        public string EnvironmentName { get; init; } = "";
        public Uri BackendBaseUrl { get; init; }
        /// <summary>
        /// How simulated input reaches the game. Only consulted for in-process targets.
        /// </summary>
        public InputBackend InputBackend { get; init; }

        /// <summary>Host for out-of-process drivers. Unused by in-process drivers.</summary>
        public string DriverHost { get; init; } = "";

        /// <summary>Port for out-of-process drivers. Unused by in-process drivers.</summary>
        public int DriverPort { get; init; }
        public float DefaultTimeout { get; init; }
        public int DefaultPollInterval { get; init; }
    }

    /// <summary>Where the game under test runs, which decides the driver.</summary>
    /// <remarks>
    /// <c>Fake</c> is appended rather than placed first so the existing members keep
    /// their numeric values, and so a config that omits the field still means Editor.
    /// </remarks>
    public enum ExecutionTarget
    {
        /// <summary>In Unity play mode, driven in-process.</summary>
        Editor,

        /// <summary>A built standalone player, driven out of process.</summary>
        StandalonePlayer,

        /// <summary>A build on real hardware, driven out of process.</summary>
        Device,

        /// <summary>No game at all — an in-memory fake. The only tier edit mode can use.</summary>
        Fake
    }

    /// <summary>
    /// Which UI technology the game's interactive objects are built from.
    /// </summary>
    /// <remarks>
    /// Deliberately separate from <see cref="ExecutionTarget"/>. That says <em>where</em> the
    /// game runs — fake, editor, player, device. This says <em>how</em> a click reaches it.
    /// They are independent: folding them into one enum would produce meaningless
    /// combinations and force a new member every time either axis grows.
    /// </remarks>
    public enum InputBackend
    {
        /// <summary>uGUI widgets, hit-tested by the graphic raycaster.</summary>
        UGui,

        /// <summary>2D sprites with colliders, hit-tested by Physics2D.</summary>
        Physics2D,

        /// <summary>3D meshes with colliders, hit-tested by Physics.</summary>
        Physics3D,

        /// <summary>UI Toolkit elements inside a UIDocument, picked by the panel.</summary>
        UiToolkit
    }

    public enum Platform
    {
        Windows,
        MacOS,
        Linux,
        Android,
        iOS
    }
}




