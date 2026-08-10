using System.Collections.Generic;
using System.IO;
using Routaz.TestGoblin.Runtime.Scenarios;
using UnityEditor;
using UnityEngine;

namespace Routaz.TestGoblin.Editor
{
    /// <summary>
    /// Editor conveniences for working with scenarios.
    /// </summary>
    public static class ScenarioToolsMenu
    {
        /// <summary>
        /// Makes Test Runner rediscover scenario files.
        /// </summary>
        /// <remarks>
        /// <para>
        /// <c>ScenarioRunnerTests</c> enumerates the scenario folder through a
        /// <c>[ValueSource]</c>, and NUnit evaluates a value source at <em>discovery</em>
        /// time. Unity caches that test list and rebuilds it on domain reload, so a scenario
        /// saved while the editor is open does not appear until a reload happens.
        /// </para>
        /// <para>
        /// There is no supported API to invalidate just the test list, so this asks for a
        /// script reload — the same thing that happens when you touch a <c>.cs</c> file, but
        /// without having to touch one. Batch mode never needs it: a fresh process discovers
        /// from scratch every run, which is why the control server's Save &amp; run works
        /// without this.
        /// </para>
        /// </remarks>
        [MenuItem("TestGoblin/Refresh Scenario Tests")]
        public static void RefreshScenarioTests()
        {
            string directory = ScenarioLoader.ResolveDirectory(Path.GetDirectoryName(Application.dataPath));
            IReadOnlyList<string> files = ScenarioLoader.ListFiles(directory);

            // Logged before the reload, so the count is visible even though everything after
            // this point is interrupted by the domain going away.
            Debug.Log($"[TestGoblin] Found {files.Count} scenario(s) in {directory}. " +
                      "Reloading scripts so Test Runner picks them up.");

            EditorUtility.RequestScriptReload();
        }

        /// <summary>
        /// Copies scenarios shipped by imported samples into this project's scenario folder.
        /// </summary>
        /// <remarks>
        /// A sample's scenarios arrive under <c>Assets/Samples/…</c>, which is not where the
        /// runner looks — scenarios belong to the project, not to a package. Rather than ask
        /// people to hand-copy files as a first experience of the tool, this installs them.
        /// <para>
        /// Existing files are left alone: a scenario someone has edited is theirs, and
        /// silently overwriting it with the sample's version would lose work.
        /// </para>
        /// </remarks>
        [MenuItem("TestGoblin/Install Sample Scenarios")]
        public static void InstallSampleScenarios()
        {
            string samplesRoot = Path.Combine(Application.dataPath, "Samples");

            if (!Directory.Exists(samplesRoot))
            {
                Debug.LogWarning("[TestGoblin] No imported samples found. Import one first.");
                return;
            }

            string destination = ScenarioLoader.ResolveDirectory(Path.GetDirectoryName(Application.dataPath));
            Directory.CreateDirectory(destination);

            int installed = 0;
            int skipped = 0;

            foreach (string source in Directory.GetFiles(
                samplesRoot, ScenarioLoader.SearchPattern, SearchOption.AllDirectories))
            {
                string target = Path.Combine(destination, Path.GetFileName(source));

                if (File.Exists(target))
                {
                    skipped++;
                    continue;
                }

                File.Copy(source, target);
                installed++;
            }

            Debug.Log($"[TestGoblin] Installed {installed} scenario(s) into {destination}" +
                      (skipped > 0 ? $"; left {skipped} existing file(s) untouched." : "."));
        }

        /// <summary>Opens the scenario folder, creating it if this project has none yet.</summary>
        /// <remarks>
        /// Useful mainly because the folder ends in <c>~</c>, which hides it from Unity's
        /// Project window — so there is no way to reach it from inside the editor otherwise.
        /// </remarks>
        [MenuItem("TestGoblin/Open Scenarios Folder")]
        public static void OpenScenariosFolder()
        {
            string directory = ScenarioLoader.ResolveDirectory(Path.GetDirectoryName(Application.dataPath));

            Directory.CreateDirectory(directory);
            EditorUtility.RevealInFinder(directory + Path.DirectorySeparatorChar);
        }
    }
}
