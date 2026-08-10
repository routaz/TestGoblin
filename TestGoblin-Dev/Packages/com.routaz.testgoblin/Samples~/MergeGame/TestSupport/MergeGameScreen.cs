using System.Threading.Tasks;
using Routaz.TestGoblin.Runtime.Driver;
using Routaz.TestGoblin.Runtime.Screens;

namespace DemoGame.Screens
{
    /// <summary>
    /// The merge game's screen: every locator it uses, and nothing else.
    /// </summary>
    /// <remarks>
    /// The object names are duplicated here rather than referenced from
    /// <c>MergeGameView</c>, deliberately. A real test tier drives a built player from
    /// another process and cannot see the game's types at all, so a Screen that compiled
    /// against them would be proving something the device tier could never do. Drift shows
    /// up as "no active object with that name", which is the honest failure.
    /// </remarks>
    public sealed class MergeGameScreen : ScreenBase
    {
        private const string SpawnButtonName = "SpawnButton";
        private const string FillEnergyButtonName = "FillEnergyButton";
        private const string LevelUpButtonName = "LevelUpButton";
        private const string EnergyLabelName = "EnergyLabel";
        private const string ExperienceLabelName = "ExperienceLabel";
        private const string LevelLabelName = "LevelLabel";
        private const string CellNameFormat = "Cell_{0}";

        /// <summary>Cells per side, mirroring the board.</summary>
        public const int Size = 4;

        /// <summary>Total cells.</summary>
        public const int CellCount = Size * Size;

        /// <summary>The Spawn button is always present, so its presence means the game is up.</summary>
        protected override string SignatureObjectName => SpawnButtonName;

        public MergeGameScreen(IGameDriver driver) : base(driver)
        {
        }

        /// <summary>Taps Spawn once.</summary>
        public async Task Spawn()
        {
            GoblinObject button = await Driver.WaitForObject(SpawnButtonName);
            await Driver.Tap(button);
        }

        /// <summary>Whether spawning is currently possible.</summary>
        public async Task<bool> CanSpawn()
        {
            GoblinObject button = await Driver.FindObject(SpawnButtonName);
            return button != null && button.Enabled;
        }

        /// <summary>Drags one cell onto another, which the game treats as a merge attempt.</summary>
        public async Task MergeCells(int from, int to)
        {
            GoblinObject source = await Driver.WaitForObject(string.Format(CellNameFormat, from));
            GoblinObject destination = await Driver.WaitForObject(string.Format(CellNameFormat, to));
            await Driver.Drag(source, destination);
        }

        /// <summary>The tier shown in a cell, or 0 when it is empty.</summary>
        public async Task<int> GetCellTier(int index)
        {
            GoblinObject cell = await Driver.WaitForObject(string.Format(CellNameFormat, index));
            string text = await Driver.GetText(cell);

            return string.IsNullOrWhiteSpace(text) ? 0 : int.Parse(text);
        }

        /// <summary>Energy remaining.</summary>
        public Task<int> GetEnergy() => ReadNumber(EnergyLabelName);

        /// <summary>Experience toward the next level.</summary>
        public Task<int> GetExperience() => ReadNumber(ExperienceLabelName);

        /// <summary>Current level.</summary>
        public Task<int> GetLevel() => ReadNumber(LevelLabelName);

        /// <summary>
        /// Whether the refill button is on screen.
        /// </summary>
        /// <remarks>
        /// The game deactivates it rather than disabling it, so absence — not a flag — is
        /// what a player sees, and what this reports.
        /// </remarks>
        public async Task<bool> IsFillEnergyOffered()
        {
            return await Driver.FindObject(FillEnergyButtonName) != null;
        }

        /// <summary>Whether the level-up button is on screen.</summary>
        public async Task<bool> IsLevelUpOffered()
        {
            return await Driver.FindObject(LevelUpButtonName) != null;
        }

        /// <summary>Taps Fill energy.</summary>
        public async Task FillEnergy()
        {
            GoblinObject button = await Driver.WaitForObject(FillEnergyButtonName);
            await Driver.Tap(button);
        }

        /// <summary>Taps Level up.</summary>
        public async Task LevelUp()
        {
            GoblinObject button = await Driver.WaitForObject(LevelUpButtonName);
            await Driver.Tap(button);
        }

        private async Task<int> ReadNumber(string objectName)
        {
            GoblinObject label = await Driver.WaitForObject(objectName);
            string text = await Driver.GetText(label);

            return int.Parse(text);
        }
    }
}
