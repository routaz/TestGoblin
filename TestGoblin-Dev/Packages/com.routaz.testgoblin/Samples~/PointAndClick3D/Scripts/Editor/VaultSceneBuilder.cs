using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemoGame.Vault.Editor
{
    /// <summary>Creates the vault scene and registers it in Build Settings.</summary>
    public static class VaultSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/Vault3D.unity";

        [MenuItem("DemoGame/Rebuild Vault (3D) Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var host = new GameObject("Vault");
            host.AddComponent<VaultView>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();

            Debug.Log($"[DemoGame] Scene written to {ScenePath} and added to Build Settings.");
        }

        /// <summary>Batch-mode entry point, so CI can regenerate the scene headlessly.</summary>
        public static void BuildFromCommandLine()
        {
            Build();
            EditorApplication.Exit(0);
        }

        private static void AddToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();

            if (scenes.Any(entry => entry.path == ScenePath))
            {
                return;
            }

            scenes.Add(new EditorBuildSettingsScene(ScenePath, enabled: true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
