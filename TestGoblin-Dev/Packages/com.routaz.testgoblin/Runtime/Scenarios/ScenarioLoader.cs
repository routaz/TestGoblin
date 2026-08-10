using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace Routaz.TestGoblin.Runtime.Scenarios
{
    /// <summary>
    /// Reads scenario files from disk.
    /// </summary>
    /// <remarks>
    /// Scenarios live in a folder ending in <c>~</c>, which keeps Unity's importer from
    /// generating .meta files for content that is data rather than assets.
    /// </remarks>
    public static class ScenarioLoader
    {
        /// <summary>Extension pattern identifying scenario files.</summary>
        public const string SearchPattern = "*.scenario.json";

        /// <summary>Environment variable overriding where scenarios are read from.</summary>
        public const string DirectoryEnvironmentVariable = "TESTGOBLIN_SCENARIOS_PATH";

        /// <summary>Default location, relative to the Unity project root.</summary>
        public const string DefaultRelativeDirectory = "TestGoblin/Scenarios~";

        /// <summary>
        /// Resolves the scenario directory: the environment override if set, else
        /// <c>TestGoblin/Scenarios~</c> in the consuming project.
        /// </summary>
        /// <param name="projectRoot">The Unity project folder (the parent of Assets).</param>
        /// <remarks>
        /// Deliberately in the <em>project</em>, not in the package. Scenarios are content
        /// a team authors and commits, so they belong to the game rather than to the
        /// framework. Keeping them in the package would also break outright once the
        /// package is consumed by git URL or from a registry: it then lives in
        /// <c>Library/PackageCache</c>, which is regenerated on resolve — so every scenario
        /// QA had written would silently disappear.
        /// </remarks>
        public static string ResolveDirectory(string projectRoot)
        {
            string overridePath = Environment.GetEnvironmentVariable(DirectoryEnvironmentVariable);

            if (!string.IsNullOrEmpty(overridePath))
            {
                return Path.GetFullPath(overridePath);
            }

            return Path.GetFullPath(Path.Combine(projectRoot, DefaultRelativeDirectory));
        }

        /// <summary>
        /// Lists scenario files in a directory, sorted for stable test ordering.
        /// </summary>
        /// <returns>Empty when the directory does not exist — an absent folder is not an error.</returns>
        public static IReadOnlyList<string> ListFiles(string directory)
        {
            if (!Directory.Exists(directory))
            {
                return Array.Empty<string>();
            }

            return Directory.GetFiles(directory, SearchPattern)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Loads and structurally validates one scenario file.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The file is not valid JSON, or is missing a name or steps.
        /// </exception>
        public static Scenario Load(string path)
        {
            string json;

            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException($"Could not read scenario '{path}': {exception.Message}");
            }

            Scenario scenario;

            try
            {
                scenario = JsonConvert.DeserializeObject<Scenario>(json);
            }
            catch (JsonException exception)
            {
                // Hand-edited scenario files are the common case, so a parse error should
                // point at the file rather than surfacing as a bare JSON exception.
                throw new InvalidOperationException($"Scenario '{path}' is not valid JSON: {exception.Message}");
            }

            if (scenario == null)
            {
                throw new InvalidOperationException($"Scenario '{path}' is empty.");
            }

            if (string.IsNullOrWhiteSpace(scenario.Name))
            {
                throw new InvalidOperationException($"Scenario '{path}' has no \"name\".");
            }

            if (scenario.Steps == null || scenario.Steps.Count == 0)
            {
                throw new InvalidOperationException($"Scenario '{scenario.Name}' ({path}) has no steps.");
            }

            scenario.SourcePath = path;

            // Normalise so the executor never has to null-check a hand-written file.
            foreach (ScenarioStep step in scenario.Steps)
            {
                step.Arguments ??= new Dictionary<string, object>();
            }

            return scenario;
        }
    }
}
