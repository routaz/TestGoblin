using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Routaz.TestGoblin.Runtime.Scenarios;
using UnityEditor;
using UnityEngine;

namespace Routaz.TestGoblin.Editor
{
    /// <summary>
    /// Writes the keyword vocabulary to JSON so the scenario builder can render it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the one-way bridge between C# and the browser: keywords are defined in code
    /// and discovered by reflection, and this file is how the UI learns what exists. Nothing
    /// authors keywords in the UI, so the C# side stays the single source of truth.
    /// </para>
    /// <para>
    /// Three entry points, one implementation: a menu item for humans, a command-line method
    /// for batch mode and the control server, and <see cref="Export"/> for anything else.
    /// </para>
    /// </remarks>
    public static class KeywordCatalogExporter
    {
        /// <summary>Command-line flag that overrides the output location.</summary>
        public const string PathArgument = "-keywordCatalogPath";

        /// <summary>File name used when no path is supplied.</summary>
        public const string DefaultFileName = "keywords.json";

        [MenuItem("TestGoblin/Export Keyword Catalog")]
        public static void ExportFromMenu()
        {
            string path = Export(DefaultPath());
            Debug.Log($"[TestGoblin] Keyword catalog written to {path}");
        }

        /// <summary>
        /// Batch-mode entry point.
        /// </summary>
        /// <remarks>
        /// Invoke as:
        /// <code>
        /// Unity -batchmode -quit -projectPath &lt;project&gt;
        ///       -executeMethod Routaz.TestGoblin.Editor.KeywordCatalogExporter.ExportFromCommandLine
        ///       -keywordCatalogPath &lt;file&gt;
        /// </code>
        /// Exits with a non-zero code on failure so a caller — CI, or the control server —
        /// can tell that it did not get a catalog, rather than reading a stale file.
        /// </remarks>
        public static void ExportFromCommandLine()
        {
            try
            {
                string path = Export(ReadPathArgument() ?? DefaultPath());
                Debug.Log($"[TestGoblin] Keyword catalog written to {path}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[TestGoblin] Keyword catalog export failed: {exception}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Writes the catalog and returns the absolute path written.
        /// </summary>
        /// <param name="outputPath">Destination file. Parent directories are created.</param>
        public static string Export(string outputPath)
        {
            KeywordRegistry registry = KeywordRegistry.FromLoadedAssemblies();

            // Shaped explicitly rather than serialising the registry, because this file is a
            // contract with the browser: it should change only when someone means it to.
            var catalog = new
            {
                generatedUtc = DateTime.UtcNow.ToString("o"),
                keywords = registry.Keywords.Select(keyword => new
                {
                    name = keyword.Name,
                    description = keyword.Description,
                    parameters = keyword.Parameters.Select(parameter => new
                    {
                        name = parameter.Name,
                        type = parameter.TypeName,
                        optional = parameter.IsOptional,
                        defaultValue = ToJsonValue(parameter.DefaultValue),
                        allowedValues = parameter.AllowedValues
                    })
                }),

                // Surfaced so a malformed keyword is visible in the browser, not only in a
                // test log. QA should be able to see that something is broken without
                // needing anyone to read Unity's console for them.
                problems = registry.Problems
            };

            string fullPath = Path.GetFullPath(outputPath);
            string directory = Path.GetDirectoryName(fullPath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(fullPath, JsonConvert.SerializeObject(catalog, Formatting.Indented));

            return fullPath;
        }

        /// <summary>
        /// Converts a parameter default into something JSON represents faithfully.
        /// </summary>
        /// <remarks>
        /// Enums would otherwise serialise as their underlying integer, and the UI needs the
        /// name to preselect the right option in a dropdown.
        /// </remarks>
        private static object ToJsonValue(object value)
        {
            if (value == null)
            {
                return null;
            }

            return value.GetType().IsEnum ? value.ToString() : value;
        }

        /// <summary>Project-root/keywords.json.</summary>
        private static string DefaultPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", DefaultFileName));
        }

        /// <summary>Reads the output path from the command line, or null if absent.</summary>
        private static string ReadPathArgument()
        {
            string[] arguments = Environment.GetCommandLineArgs();

            for (int index = 0; index < arguments.Length - 1; index++)
            {
                if (arguments[index] == PathArgument)
                {
                    return arguments[index + 1];
                }
            }

            return null;
        }
    }
}
