using System;
using System.Threading.Tasks;
using DemoGame.Screens;
using Routaz.TestGoblin.Runtime.Driver;
using Routaz.TestGoblin.Runtime.Scenarios;

namespace DemoGame.Flows
{
    /// <summary>
    /// The merge game's keyword vocabulary — the verbs a QA engineer composes scenarios from.
    /// </summary>
    /// <remarks>
    /// Per-title code. TestGoblin discovers these by scanning for
    /// <see cref="ScenarioActionAttribute"/>, so the framework never names this class or
    /// its assembly. Every action is intent level: a scenario says "merge everything you
    /// can", never "drag cell 3 onto cell 7".
    /// </remarks>
    public sealed class MergeGameFlows
    {
        private readonly MergeGameScreen _screen;
        private readonly LoginScreen _login;

        /// <summary>
        /// The constructor shape <c>ScenarioExecutor</c> requires of any class declaring
        /// scenario actions.
        /// </summary>
        public MergeGameFlows(IGameDriver driver)
        {
            _screen = new MergeGameScreen(driver);
            _login = new LoginScreen(driver);
        }

        [ScenarioAction("LogIn",
            Description = "Logs in and waits for the game screen, failing if loading takes longer than the budget.")]
        public async Task LogIn(float maxLoadingSeconds = 5f)
        {
            await _login.WaitUntilVisible();
            await _login.TapLogIn();

            // The budget is a product requirement expressed in the scenario, not a driver
            // setting: QA decides how long a player should be made to wait, and a build that
            // regresses past it fails the step rather than merely being slow.
            await _login.WaitUntilGameReady(maxLoadingSeconds);
        }

        [ScenarioAction("Spawn",
            Description = "Spawns items one at a time, spending one energy each.")]
        public async Task Spawn(int count = 1)
        {
            for (int spawned = 0; spawned < count; spawned++)
            {
                if (!await _screen.CanSpawn())
                {
                    throw new InvalidOperationException(
                        $"Ran out of energy after {spawned} of {count} spawns.");
                }

                await _screen.Spawn();
            }
        }

        [ScenarioAction("SpawnUntilEnergyEmpty",
            Description = "Spawns items until the energy bar is spent.")]
        public async Task SpawnUntilEnergyEmpty()
        {
            // Bounded by the energy actually shown, so a driver that silently fails to tap
            // reports "spawning made no progress" rather than spinning forever.
            int budget = await _screen.GetEnergy();

            for (int spawned = 0; spawned <= budget; spawned++)
            {
                if (!await _screen.CanSpawn())
                {
                    return;
                }

                await _screen.Spawn();
            }

            throw new InvalidOperationException(
                $"Spawned {budget + 1} times but energy never reached zero — the Spawn button may not be responding.");
        }

        [ScenarioAction("MergeAllPossible",
            Description = "Merges pairs until no two items on the board match.")]
        public async Task MergeAllPossible()
        {
            // Every merge removes exactly one item, so at most CellCount merges are possible.
            // The bound is a guard against a broken driver, not against the game's rules.
            for (int merges = 0; merges <= MergeGameScreen.CellCount; merges++)
            {
                (int From, int To)? pair = await FindMergeablePair();

                if (pair == null)
                {
                    return;
                }

                await _screen.MergeCells(pair.Value.From, pair.Value.To);
            }

            throw new InvalidOperationException(
                "Merging never ran out of pairs — drags may not be reaching the board.");
        }

        [ScenarioAction("FillEnergy",
            Description = "Taps Fill energy. Fails if the button is not being offered.")]
        public async Task FillEnergy()
        {
            if (!await _screen.IsFillEnergyOffered())
            {
                throw new InvalidOperationException(
                    $"Fill energy is not offered — energy is {await _screen.GetEnergy()}.");
            }

            await _screen.FillEnergy();
        }

        [ScenarioAction("LevelUp",
            Description = "Taps Level up. Fails if not enough experience has been earned.")]
        public async Task LevelUp()
        {
            if (!await _screen.IsLevelUpOffered())
            {
                throw new InvalidOperationException(
                    $"Level up is not offered — experience is {await _screen.GetExperience()}.");
            }

            await _screen.LevelUp();
        }

        [ScenarioAction("PlayUntilLevelUpReady",
            Description = "Spawns, merges and refills energy until levelling up becomes possible.")]
        public async Task PlayUntilLevelUpReady(int maxRefills = 5)
        {
            // Bounded on purpose. Without the limit a game that can never reach the
            // threshold would hang until CI times out, reporting nothing useful; with it,
            // the failure says how far it got.
            for (int refills = 0; refills <= maxRefills; refills++)
            {
                await SpawnUntilEnergyEmpty();
                await MergeAllPossible();

                if (await _screen.IsLevelUpOffered())
                {
                    return;
                }

                if (!await _screen.IsFillEnergyOffered())
                {
                    throw new InvalidOperationException(
                        "Cannot level up and cannot refill energy — the game is stuck at " +
                        $"{await _screen.GetExperience()} experience.");
                }

                await _screen.FillEnergy();
            }

            throw new InvalidOperationException(
                $"Level up was still unavailable after {maxRefills} refills; " +
                $"experience reached {await _screen.GetExperience()}.");
        }

        /// <summary>
        /// Reads the whole board through the UI and returns the first matching pair.
        /// </summary>
        /// <remarks>
        /// Scans in a fixed order so repeated runs merge in the same sequence — the game is
        /// deterministic, and the flow must not be the thing that reintroduces variance.
        /// </remarks>
        private async Task<(int From, int To)?> FindMergeablePair()
        {
            var tiers = new int[MergeGameScreen.CellCount];

            for (int index = 0; index < tiers.Length; index++)
            {
                tiers[index] = await _screen.GetCellTier(index);
            }

            for (int to = 0; to < tiers.Length; to++)
            {
                if (tiers[to] == 0)
                {
                    continue;
                }

                for (int from = to + 1; from < tiers.Length; from++)
                {
                    if (tiers[from] == tiers[to])
                    {
                        return (from, to);
                    }
                }
            }

            return null;
        }
    }
}
