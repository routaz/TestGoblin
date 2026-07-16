using Routaz.TestGoblin.Runtime.Driver;
using System.Threading.Tasks;

namespace Routaz.TestGoblin.Runtime.Flows
{
    public class GameFlows
    {
        private IGameDriver Driver { get; }
        private const int FullEnergyLevel = 5; // TODO: verify against real build.

        public GameFlows(IGameDriver driver)
        {
            Driver = driver;
        }

        public async Task CompleteTutorial()
        {
            var tutorialScreen = new Screens.TutorialScreen(Driver);
            await tutorialScreen.WaitUntilVisible();
            // TODO: iterate all tutorial steps
            await tutorialScreen.CompleteStep();
        }

        public async Task ClaimDailyReward()
        {
            var dailyRewardScreen = new Screens.DailyRewardPopup(Driver);
            await dailyRewardScreen.WaitUntilVisible();
            await dailyRewardScreen.Claim();
            await dailyRewardScreen.WaitUntilClosed();
        }

        public async Task PlayUntilEnergyFull()
        {
            var gameBoardScreen = new Screens.GameBoardScreen(Driver);
            while (await gameBoardScreen.GetEnergyLevel() < FullEnergyLevel)
            {
                // TODO: real move selection - placeholder move for now.
                await gameBoardScreen.DragGem(0, 0, 1, 0);
            }
        }
    }
}
