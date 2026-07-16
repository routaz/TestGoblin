
using Routaz.TestGoblin.Runtime.Driver;
using System.Threading.Tasks;

namespace Routaz.TestGoblin.Runtime.Screens
{
    public class GameBoardScreen : ScreenBase
    {
        private const string GameBoardScreenName = "GameBoardScreen"; // TODO(verify): confirm against instrumented build.
        private const string EnergyLevelName = "EnergyLevel"; // TODO(verify): confirm against instrumented build.
        private const string SpecialMoveButtonName = "SpecialMoveButton"; // TODO(verify): confirm against instrumented build.
        private const string GemNameTemplate = "Gem_{0}_{1}"; // TODO(verify): confirm gem naming scheme against instrumented build.

        protected override string SignatureObjectName => GameBoardScreenName;

        public GameBoardScreen(IGameDriver gameDriver) : base(gameDriver) { }

        public async Task DragGem(int fromColumn, int fromRow, int toColumn, int toRow)
        {
            GoblinObject sourceGem = await Driver.WaitForObject(string.Format(GemNameTemplate, fromColumn, fromRow));
            GoblinObject destinationGem = await Driver.WaitForObject(string.Format(GemNameTemplate, toColumn, toRow));
            await Driver.Drag(sourceGem, destinationGem);
        }

        public async Task<int> GetEnergyLevel()
        {
            GoblinObject energyLabel = await Driver.WaitForObject(EnergyLevelName);
            string text = await Driver.GetText(energyLabel);
            // TODO(verify): label format against real build — assumes a plain integer, e.g. "3".
            return int.Parse(text);
        }

        public async Task<bool> IsSpecialMoveAvailable()
        {
            // Query, not expectation: FindObject checks "right now" without
            // waiting out a timeout, so an unavailable move returns false fast.
            GoblinObject specialMoveButton = await Driver.FindObject(SpecialMoveButtonName);
            return specialMoveButton != null && specialMoveButton.Enabled;
        }
    }
}
