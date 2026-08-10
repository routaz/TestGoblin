using System.Collections.Generic;

namespace Routaz.TestGoblin.Runtime.Scenarios
{
    /// <summary>
    /// One step of a scenario: a keyword and the arguments to call it with.
    /// </summary>
    public sealed class ScenarioStep
    {
        /// <summary>The keyword name, matching a <see cref="ScenarioActionAttribute"/>.</summary>
        public string Action { get; set; }

        /// <summary>
        /// Arguments by parameter name. Omitted parameters must have defaults.
        /// </summary>
        /// <remarks>
        /// Named rather than positional so that adding an optional parameter to a keyword
        /// cannot silently change what every existing scenario means.
        /// </remarks>
        public Dictionary<string, object> Arguments { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// A test case authored as data: an ordered list of keyword invocations.
    /// </summary>
    /// <remarks>
    /// Deliberately not translated into generated C#. Scenarios stay data all the way to
    /// execution, so saving one requires no compile, no domain reload, and no Unity editor.
    /// </remarks>
    public sealed class Scenario
    {
        /// <summary>Human-readable name, shown as the test name.</summary>
        public string Name { get; set; }

        /// <summary>Optional longer explanation of what this scenario covers.</summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Overrides the run's input backend for this scenario. Empty means "use whatever
        /// the run is configured for".
        /// </summary>
        /// <remarks>
        /// <para>
        /// One name — <c>"UGui"</c>, <c>"Physics2D"</c>, <c>"Physics3D"</c>,
        /// <c>"UiToolkit"</c> — or a comma-separated stack ordered <b>topmost first</b>, such
        /// as <c>"UiToolkit,Physics3D"</c> for a HUD drawn over a 3D world. A stack composes
        /// the drivers so a click is refused when an upper layer covers the target.
        /// </para>
        /// <para>
        /// Per-scenario because a project can legitimately contain more than one kind. A
        /// scenario already names the scene it drives, and what that scene's clickable things
        /// are built from is the same sort of fact about it.
        /// </para>
        /// </remarks>
        public string InputBackend { get; set; } = "";

        /// <summary>
        /// Scene to load before the first step. Empty means "use whatever is already loaded".
        /// </summary>
        /// <remarks>
        /// Per-scenario rather than fixed by the runner, because a suite that can only ever
        /// start in one scene is not a suite. The scene must be in Build Settings.
        /// </remarks>
        public string Scene { get; set; } = "";

        /// <summary>The steps, in order.</summary>
        public List<ScenarioStep> Steps { get; set; } = new List<ScenarioStep>();

        /// <summary>
        /// Where this scenario was loaded from. Not serialized; set by the loader so
        /// failures can name the file rather than only the scenario.
        /// </summary>
        public string SourcePath { get; set; } = "";

        public override string ToString()
        {
            return string.IsNullOrEmpty(Name) ? "(unnamed scenario)" : Name;
        }
    }
}
