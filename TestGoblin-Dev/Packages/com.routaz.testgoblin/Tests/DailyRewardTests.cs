using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Routaz.TestGoblin.Runtime.Backend;
using Routaz.TestGoblin.Runtime.Flows;

namespace Routaz.TestGoblin.Tests
{
    public class DailyRewardTests : BaseTest
    {
        private const string DailyRewardPopupName = "DailyRewardPopup";
        private const string ClaimButtonName = "ClaimButton";
        private const string RewardItemKey = "DailyReward";

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
            fake.ScriptObject(DailyRewardPopupName);
            fake.ScriptObject(ClaimButtonName);

            // The claim flow ends by waiting for the popup to close; simulate the
            // game dismissing it shortly after the claim so WaitForObjectGone resolves.
            _ = Task.Run(async () =>
            {
                await Task.Delay(100);
                fake.RemoveScriptedObject(DailyRewardPopupName);
            });

            // Act: drive the UI claim, then simulate the backend granting the reward.
            // (Fake UI and fake backend are deliberately decoupled — the real game's
            // claim button would hit the real backend.)
            var flow = new GameFlows(Driver);
            await flow.ClaimDailyReward();
            ((FakeBackendTestApi)Backend).GrantItem(RewardItemKey, 1);

            // Assert on state read back through the backend seam, not the local seed.
            IReadOnlyDictionary<string, int> inventory = await Backend.GetInventory();
            Assert.AreEqual(1, inventory[RewardItemKey]);
        }
    }
}
