using System.Collections;
using System.Diagnostics;
using System.Threading.Tasks;
using DemoGame.Screens;
using NUnit.Framework;
using Routaz.TestGoblin.Runtime.Driver;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DemoGame.Tests.PlayMode
{
    /// <summary>
    /// The framework waiting for a screen transition that genuinely takes time.
    /// </summary>
    /// <remarks>
    /// Every other test in this project acts on state that changes within a single frame,
    /// so the driver's polling waits were never really exercised — a driver that did no
    /// waiting at all would have passed them. Loading takes 1.5 seconds, so reaching the
    /// game screen is only possible by actually waiting for it.
    /// </remarks>
    [Category("LiveScene")]
    public class LoginFlowTests
    {
        private const string SceneName = "MergeGame";
        private const float Timeout = 10f;
        private const int PollIntervalMs = 100;

        [UnityTest]
        public IEnumerator LoggingIn_ShowsLoading_ThenTheGameScreen()
        {
            SceneManager.LoadScene(SceneName);
            yield return null;
            yield return null;

            yield return RunAsync(DriveAsync());
        }

        private static async Task DriveAsync()
        {
            var driver = new InProcessGameDriver(Timeout, PollIntervalMs);
            var login = new LoginScreen(driver);

            await login.WaitUntilVisible();

            Assert.IsFalse(await login.IsGameVisible(),
                "The game must not be reachable before logging in.");

            await login.TapLogIn();

            // Immediately after the tap the app should be loading, not finished. If this
            // fails, the transition is instant and the wait below proves nothing.
            Assert.IsTrue(await login.IsLoading(),
                "Loading should be on screen straight after logging in.");
            Assert.IsFalse(await login.IsGameVisible(),
                "The game should not appear until loading completes.");

            // The budget: loading must finish within this, or the wait times out and the
            // test fails. Deliberately close to the real 1.5s load rather than generous —
            // a budget nothing could ever exceed is not a budget.
            const float LoadingBudgetSeconds = 3f;

            var stopwatch = Stopwatch.StartNew();
            await login.WaitUntilGameReady(LoadingBudgetSeconds);
            stopwatch.Stop();

            Assert.IsTrue(await login.IsGameVisible(), "The game screen should be up.");
            Assert.IsFalse(await login.IsLoading(), "The loading screen should be gone.");

            // The upper bound is enforced by the wait itself — exceeding it throws before
            // reaching here. This lower bound is the complement, and guards something else
            // entirely: that the wait actually waited. A driver that returned immediately
            // would satisfy any budget trivially, and no other test in the suite would notice.
            Assert.Greater(stopwatch.ElapsedMilliseconds, 500L,
                "Reaching the game screen should have required waiting out the load.");
        }

        private static IEnumerator RunAsync(Task task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                throw task.Exception?.InnerException ?? task.Exception;
            }
        }
    }
}
