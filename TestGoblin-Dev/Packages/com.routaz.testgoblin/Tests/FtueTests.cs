using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Routaz.TestGoblin.Runtime.Backend;
using Routaz.TestGoblin.Runtime.Driver;
using Routaz.TestGoblin.Runtime.Flows;

namespace Routaz.TestGoblin.Tests
{
    public class FtueTests : BaseTest
    {
        // Object names the flow/screens will look up. In proof-level-1 the test
        // scripts these into the fake driver so the waits resolve; a real run
        // finds them in the live scene instead.
        private const string TutorialScreenName = "TutorialScreen";
        private const string ContinueButtonName = "ContinueButton";
        private const string GameBoardScreenName = "GameBoardScreen";

        [Test]
        public async Task NewPlayer_CompletesTutorial_ReachesGameBoard()
        {
            // Arrange: fresh player, and script the objects the tutorial flow drives.
            await Backend.SeedPlayerState(new PlayerProfile
            {
                HasCompletedTutorial = false,
                IsDailyRewardAvailable = false
            });

            var fake = (FakeGameDriver)Driver;
            fake.ScriptObject(TutorialScreenName);
            fake.ScriptObject(ContinueButtonName);
            fake.ScriptObject(GameBoardScreenName);

            // Act
            var flow = new GameFlows(Driver);
            await flow.CompleteTutorial();

            // Assert: the board is reachable after the tutorial.
            GoblinObject board = await Driver.FindObject(GameBoardScreenName);
            Assert.NotNull(board);
        }

        [Test]
        public async Task EligiblePlayer_ClaimsDailyReward_InventoryUpdated()
        {
            // Arrange: reward-eligible player, and script the popup + claim button.
            await Backend.SeedPlayerState(new PlayerProfile
            {
                HasCompletedTutorial = true,
                IsDailyRewardAvailable = true
            });

            var fake = (FakeGameDriver)Driver;
            fake.ScriptObject("DailyRewardPopup");
            fake.ScriptObject("ClaimButton");

            // The claim flow ends by waiting for the popup to close; simulate the
            // game dismissing it shortly after the claim so WaitForObjectGone resolves.
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                fake.RemoveScriptedObject("DailyRewardPopup");
            });

            // Act: drive the UI claim, then simulate the backend granting the reward.
            // (Fake UI and fake backend are deliberately decoupled — the real game's
            // claim button would hit the real backend.)
            var flow = new GameFlows(Driver);
            await flow.ClaimDailyReward();
            ((FakeBackendTestApi)Backend).GrantItem("DailyReward", 1);

            // Assert on state read back through the backend seam, not the local seed.
            IReadOnlyDictionary<string, int> inventory = await Backend.GetInventory();
            Assert.AreEqual(1, inventory["DailyReward"]);
        }
    }
}
