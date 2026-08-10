using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;

namespace Routaz.TestGoblin.Editor
{
    /// <summary>
    /// Imports the package's samples without going through the Package Manager window.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Samples live in <c>Samples~</c>, which Unity does not compile — so a sample can rot
    /// silently, which is exactly how the AltTester driver ended up unverified. CI therefore
    /// imports them and compiles them like any other code, and this is the entry point that
    /// makes that scriptable.
    /// </para>
    /// <para>
    /// It uses the same <see cref="Sample.Import"/> the Package Manager window calls, so what
    /// CI exercises is the real consumer path rather than a private copy of it.
    /// </para>
    /// </remarks>
    public static class SampleImporter
    {
        private const string PackageName = "com.routaz.testgoblin";

        [MenuItem("TestGoblin/Import All Samples")]
        public static void ImportAll()
        {
            int imported = Import();
            Debug.Log($"[TestGoblin] Imported {imported} sample(s) into Assets/Samples.");
        }

        /// <summary>
        /// Batch-mode entry point: import every sample, then exit.
        /// </summary>
        /// <remarks>
        /// Exits non-zero when nothing was imported. A silent "0 samples" would let CI run a
        /// suite that quietly tests nothing, which is worse than failing.
        /// </remarks>
        public static void ImportAllFromCommandLine()
        {
            int imported = Import();

            if (imported == 0)
            {
                Debug.LogError($"[TestGoblin] No samples found in {PackageName}.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[TestGoblin] Imported {imported} sample(s).");
            AssetDatabase.Refresh();
            EditorApplication.Exit(0);
        }

        private static int Import()
        {
            // Empty version means "whatever version is resolved", which is what an embedded
            // package has. Passed positionally: the parameter's name differs across Unity versions.
            List<Sample> samples = Sample.FindByPackage(PackageName, string.Empty).ToList();

            foreach (Sample sample in samples)
            {
                // overwrite: true so a re-import picks up edits made in Samples~, which is
                // where the sample is actually maintained.
                sample.Import(Sample.ImportOptions.OverridePreviousImports);
                Debug.Log($"[TestGoblin] Imported sample '{sample.displayName}'.");
            }

            return samples.Count;
        }
    }
}
