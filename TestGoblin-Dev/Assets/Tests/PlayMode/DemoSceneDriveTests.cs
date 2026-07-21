using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Routaz.TestGoblin.Runtime.Config;
using Routaz.TestGoblin.Runtime.Driver;
using Routaz.TestGoblin.Runtime.Flows;

namespace Routaz.TestGoblin.PlayModeDemo
{
    /// <summary>
    /// Proof level 2: the REAL AltTesterGameDriver drives the DemoGame scene
    /// through the same Screens/Flows the fake-driver tests use — the only
    /// difference from FtueTests is which IGameDriver is behind the seam.
    ///
    /// TODO(license): this cannot actually run until an AltTester Desktop license
    /// is available — AltTester 2.x routes the driver connection through the
    /// Desktop app, which requires one. Everything below is written and compiles;
    /// when the license lands, work through the TODO(license) markers and confirm
    /// the connection + scene-load mechanism, then this flips the README's
    /// "live scene" row to a demonstrable pass.
    /// </summary>
    [Category("LiveScene")]
    public class DemoSceneDriveTests
    {
        private const string DemoSceneName = "DemoGame";
        private const string GameBoardScreenName = "GameBoardScreen";

        [UnityTest]
        public IEnumerator NewPlayer_CompletesTutorial_ReachesGameBoard_LiveScene()
        {
            // TODO(license): the DemoGame scene must load in play mode WITH the
            // AltTester server running. Confirm the scene is in Build Settings so
            // LoadScene(name) works, and that AltTester's server actually starts
            // in this play session (it may require running via the AltTester
            // Editor "Play in Editor" flow, or the AltTester prefab being present
            // in the scene — verify which once we can connect).
            SceneManager.LoadScene(DemoSceneName);
            yield return null; // let the scene load and the server come up

            // Bridge our Task-based driver into UTF's coroutine runner: run the
            // async body, yield until it finishes (keeping Unity pumping frames so
            // AltTester can respond), then surface any failure. This is the
            // OPPOSITE of the edit-mode deadlock — here the main thread keeps
            // pumping instead of blocking.
            Task task = DriveAsync();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                // Unwrap so the real exception (not AggregateException) is reported.
                throw task.Exception?.InnerException ?? task.Exception;
            }
        }

        private async Task DriveAsync()
        {
            // Minimal config for the in-editor AltTester server. We construct
            // AltTesterGameDriver DIRECTLY here, deliberately bypassing BaseTest's
            // CreateDriver (which maps Editor -> FakeGameDriver) — for this demo we
            // want the real driver running against the live editor scene.
            // TODO(license): confirm host/port match the AltTester Editor settings
            // (default 127.0.0.1:13000) and that the server is listening.
            var config = new TestRunConfig
            {
                AltTesterHost = "127.0.0.1",
                AltTesterPort = 13000,
                DefaultTimeout = 10f,
                DefaultPollInterval = 500
            };

            var driver = new AltTesterGameDriver(config);

            // TODO(license): Connect may need extra AltTester args (app name, etc.)
            // — verify against the running Desktop connection.
            await driver.Connect(config.DeviceId, config.AltTesterHost, config.AltTesterPort);

            try
            {
                // The SAME Screens/Flows the fake-driver FTUE test uses, now against
                // the real scene. Tapping ContinueButton (via CompleteTutorial) fires
                // DemoGameController, which hides the tutorial overlay and activates
                // GameBoardScreen — which FindObject then resolves.
                var flow = new GameFlows(driver);
                await flow.CompleteTutorial();

                GoblinObject board = await driver.FindObject(GameBoardScreenName);
                Assert.NotNull(board, "GameBoardScreen should be visible after completing the tutorial.");
            }
            finally
            {
                await driver.Disconnect();
            }
        }
    }
}
