using System.Collections.Generic;
using System.Linq;
using DemoGame.Merge;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DemoGame.Editor
{
    /// <summary>
    /// Creates the merge game's scene and registers it in Build Settings.
    /// </summary>
    /// <remarks>
    /// The scene is generated rather than authored because everything in it is built by
    /// <see cref="MergeGameView"/> at runtime — so the asset holds one GameObject and a
    /// camera, and the interesting part stays reviewable C#. Generating it also means the
    /// scene can be rebuilt from scratch in batch mode if it is ever lost or corrupted.
    /// </remarks>
    public static class MergeGameSceneBuilder
    {
        public const string SceneName = "MergeGame";
        public const string ScenePath = "Assets/Scenes/MergeGame.unity";

        [MenuItem("DemoGame/Rebuild Merge Game Scene")]
        public static void Build()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            var host = new GameObject("MergeGame");
            host.AddComponent<MergeGameView>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddToBuildSettings();

            Debug.Log($"[DemoGame] Scene written to {ScenePath} and added to Build Settings.");
        }

        /// <summary>Batch-mode entry point, so CI can regenerate the scene without the editor UI.</summary>
        public static void BuildFromCommandLine()
        {
            Build();
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Ensures the scene is in Build Settings, which is what SceneManager.LoadScene
        /// requires at play time.
        /// </summary>
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
