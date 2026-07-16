

using Routaz.TestGoblin.Runtime.Driver;
using System.Threading.Tasks;

namespace Routaz.TestGoblin.Runtime.Screens
{
    public class DailyRewardPopup : ScreenBase
    {
        private const string DailyRewardPopupName = "DailyRewardPopup"; // TODO(verify): confirm against instrumented build.
        private const string ClaimButtonName = "ClaimButton"; // TODO(verify): confirm against instrumented build.

        protected override string SignatureObjectName => DailyRewardPopupName;

        public DailyRewardPopup(IGameDriver driver) : base(driver){ }

        public async Task Claim()
        {
            GoblinObject claimButton = await Driver.WaitForObject(ClaimButtonName);
            await Driver.Tap(claimButton);
        }

        public Task WaitUntilClosed()
        {
            return Driver.WaitForObjectGone(SignatureObjectName);
        }
    }
}