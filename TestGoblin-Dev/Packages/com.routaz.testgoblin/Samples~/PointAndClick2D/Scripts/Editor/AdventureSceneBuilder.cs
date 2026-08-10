using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemoGame.Adventure.Editor
{
    /// <summary>
    /// Creates the point-and-click scene and registers it in Build Settings.
    /// </summary>
    /// <remarks>
    /// Generated rather than shipped: everything in it is built by <see cref="AdventureView"/>
    /// at runtime, so the asset holds one GameObject and a camera. Generating it also means
    /// the scene lands in the consuming project with valid references.
    /// </remarks>
    public static class AdventureSceneBuilder
    {
        public const string ScenePath = "Assets/Scenes/PointAndClick.unity";

        [MenuItem("DemoGame/Rebuild Point And Click Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var host = new GameObject("PointAndClick");
            host.AddComponent<AdventureView>();

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
