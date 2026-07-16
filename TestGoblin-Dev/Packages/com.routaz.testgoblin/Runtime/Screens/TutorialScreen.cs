
using Routaz.TestGoblin.Runtime.Driver;
using System.Threading.Tasks;

namespace Routaz.TestGoblin.Runtime.Screens
{
    public class TutorialScreen : ScreenBase
    {

        private const string TutorialScreenName = "TutorialScreen"; // TODO(verify): confirm against instrumented build.
        private const string ContinueButtonName = "ContinueButton"; // TODO(verify): confirm against instrumented build.

        protected override string SignatureObjectName => TutorialScreenName;

        public TutorialScreen(IGameDriver gameDriver) : base(gameDriver){}

        public async Task CompleteStep()
        {
            GoblinObject continueButton = await Driver.WaitForObject(ContinueButtonName);
            await Driver.Tap(continueButton);
        }

    }
}