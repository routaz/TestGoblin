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
    }
}
