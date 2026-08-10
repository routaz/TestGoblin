using System.Collections;
using System.Threading.Tasks;
using DemoGame.Vault.Screens;
using NUnit.Framework;
using Routaz.TestGoblin.Runtime.Driver;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace DemoGame.Vault.Tests.PlayMode
{
    /// <summary>
    /// The reason this sample is 3D: an object the player cannot reach must not be clickable,
    /// even though the game's own rules permit the action.
    /// </summary>
    /// <remarks>
    /// <see cref="VaultState.CanTakeKey"/> says nothing about the wall — taking the key is
    /// legal from the game's point of view at all times. What prevents it is geometry, and
    /// only a driver that raycasts can know. If <c>Physics3DGameDriver</c> stopped checking
    /// reachability, the game would keep working and this test would be the one thing to notice.
    /// </remarks>
    [Category("LiveScene")]
    public class VaultOcclusionTests
    {
        private const string SceneName = "Vault3D";
        private const float Timeout = 5f;
        private const int PollIntervalMs = 50;

        [UnityTest]
        public IEnumerator TakingTheKey_IsRefusedWhileTheWallIsInTheWay()
        {
            yield return LoadScene();

            var screen = new VaultScreen(new Physics3DGameDriver(Timeout, PollIntervalMs));
            Task click = screen.ClickKey();

            while (!click.IsCompleted)
            {
                yield return null;
            }

            Assert.IsTrue(click.IsFaulted,
                "The key is behind the wall — the driver must refuse, even though the game would allow it.");

            StringAssert.Contains("Wall", click.Exception?.InnerException?.Message ?? string.Empty,
                "The message must name what is in the way.");
        }

        [UnityTest]
        public IEnumerator TakingTheKey_SucceedsOnceTheLeverRetractsTheWall()
        {
            yield return LoadScene();

            var screen = new VaultScreen(new Physics3DGameDriver(Timeout, PollIntervalMs));

            yield return RunAsync(TakeAfterLever(screen));
        }

        [UnityTest]
        public IEnumerator TheGate_IsUnreachableWhileTheInventoryCoversIt()
        {
            yield return LoadScene();

            // Both technologies at once, ordered as they are drawn: HUD over world.
            var driver = new CompositeGameDriver(
                Timeout, PollIntervalMs,
                new UiToolkitGameDriver(Timeout, PollIntervalMs),
                new Physics3DGameDriver(Timeout, PollIntervalMs));

            var screen = new VaultScreen(driver);

            Task open = OpenInventoryThenClickGate(screen);

            while (!open.IsCompleted)
            {
                yield return null;
            }

            Assert.IsTrue(open.IsFaulted,
                "With the inventory open the gate is behind the interface — neither the 3D " +
                "driver nor the UI driver could tell on its own.");

            StringAssert.Contains("InventoryPanel", open.Exception?.InnerException?.Message ?? string.Empty,
                "The message must name the interface element that is in the way.");
        }

        private static async Task OpenInventoryThenClickGate(VaultScreen screen)
        {
            await screen.WaitUntilVisible();
            await screen.ToggleInventory();
            await screen.ClickGate();
        }

        private static async Task TakeAfterLever(VaultScreen screen)
        {
            await screen.WaitUntilVisible();
            await screen.ClickLever();
            await screen.ClickKey();

            Assert.IsFalse(await screen.IsKeyStillThere(),
                "The same click that was refused a moment ago should now work.");
        }

        /// <summary>
        /// Clears the scene this fixture loaded.
        /// </summary>
        /// <remarks>
        /// A loaded scene outlives its test. Leaving the vault standing — complete with an
        /// inventory panel someone opened — makes later tests fail for reasons that have
        /// nothing to do with them.
        /// </remarks>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                UnityEngine.Object.Destroy(root);
            }

            yield return null;
        }

        private static IEnumerator LoadScene()
        {
            SceneManager.LoadScene(SceneName);

            // One frame to commit the load, one for Awake to build the room.
            yield return null;
            yield return null;
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
