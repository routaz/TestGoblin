using System;
using System.Collections.Generic;

namespace DemoGame.Merge
{
    /// <summary>
    /// The merge game's rules, with no Unity dependency.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kept free of MonoBehaviour so the rules can be unit-tested directly, without a
    /// scene and without TestGoblin. That matters: the framework is verified by driving
    /// this game, so the game's own correctness has to be established independently or
    /// the argument is circular.
    /// </para>
    /// <para>
    /// Fully deterministic by construction — every spawn is the same tier and lands in
    /// the lowest empty cell. There is no randomness to seed, so a scenario that spawns
    /// and merges produces identical state on every run.
    /// </para>
    /// </remarks>
    public sealed class MergeBoard
    {
        /// <summary>Cells per side.</summary>
        public const int Size = 4;

        /// <summary>Total cells.</summary>
        public const int CellCount = Size * Size;

        /// <summary>Tier every spawned item starts at.</summary>
        public const int SpawnTier = 1;

        /// <summary>Marks a cell with no item.</summary>
        public const int Empty = 0;

        private readonly int[] _tiers = new int[CellCount];

        /// <summary>Spawns remaining before a refill is needed.</summary>
        public int Energy { get; private set; }

        /// <summary>Experience toward the next level.</summary>
        public int Experience { get; private set; }

        /// <summary>Current level, starting at 1.</summary>
        public int Level { get; private set; } = 1;

        /// <summary>Experience granted per merge, regardless of tier.</summary>
        public int ExperiencePerMerge { get; }

        /// <summary>Experience needed for the next level.</summary>
        public int ExperienceToLevelUp { get; }

        /// <summary>Energy restored by a refill.</summary>
        public int EnergyPerRefill { get; }

        public MergeBoard(
            int startingEnergy = 6,
            int experiencePerMerge = 10,
            int experienceToLevelUp = 50,
            int energyPerRefill = 6)
        {
            Energy = startingEnergy;
            ExperiencePerMerge = experiencePerMerge;
            ExperienceToLevelUp = experienceToLevelUp;
            EnergyPerRefill = energyPerRefill;
        }

        /// <summary>Tier in each cell; <see cref="Empty"/> where there is no item.</summary>
        public IReadOnlyList<int> Tiers => _tiers;

        /// <summary>Tier in one cell.</summary>
        public int TierAt(int index)
        {
            RequireInRange(index, nameof(index));
            return _tiers[index];
        }

        /// <summary>Number of cells holding an item.</summary>
        public int ItemCount
        {
            get
            {
                int count = 0;

                foreach (int tier in _tiers)
                {
                    if (tier != Empty)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        /// <summary>Whether a spawn would succeed: energy remaining and a free cell.</summary>
        public bool CanSpawn => Energy > 0 && FirstEmptyCell() >= 0;

        /// <summary>
        /// Spawns one item, spending one energy.
        /// </summary>
        /// <returns>The cell it landed in.</returns>
        /// <exception cref="InvalidOperationException">No energy, or no free cell.</exception>
        public int Spawn()
        {
            if (Energy <= 0)
            {
                throw new InvalidOperationException("Cannot spawn: energy is empty.");
            }

            int index = FirstEmptyCell();

            if (index < 0)
            {
                throw new InvalidOperationException("Cannot spawn: the board is full.");
            }

            _tiers[index] = SpawnTier;
            Energy--;

            return index;
        }

        /// <summary>Whether these two cells hold different items of the same tier.</summary>
        public bool CanMerge(int from, int to)
        {
            RequireInRange(from, nameof(from));
            RequireInRange(to, nameof(to));

            return from != to
                && _tiers[from] != Empty
                && _tiers[from] == _tiers[to];
        }

        /// <summary>
        /// Merges one item into another, promoting the destination a tier.
        /// </summary>
        /// <exception cref="InvalidOperationException">The pair cannot merge.</exception>
        public void Merge(int from, int to)
        {
            if (!CanMerge(from, to))
            {
                throw new InvalidOperationException(
                    $"Cannot merge cell {from} (tier {TierAt(from)}) into cell {to} (tier {TierAt(to)}).");
            }

            _tiers[to]++;
            _tiers[from] = Empty;
            Experience += ExperiencePerMerge;
        }

        /// <summary>
        /// Finds any mergeable pair, or null when none remains.
        /// </summary>
        /// <remarks>
        /// Scans in a fixed order, so "merge everything" is deterministic rather than
        /// dependent on which pair happens to be found first.
        /// </remarks>
        public (int From, int To)? FindMergeablePair()
        {
            for (int to = 0; to < CellCount; to++)
            {
                if (_tiers[to] == Empty)
                {
                    continue;
                }

                for (int from = to + 1; from < CellCount; from++)
                {
                    if (_tiers[from] == _tiers[to])
                    {
                        return (from, to);
                    }
                }
            }

            return null;
        }

        /// <summary>The refill button is offered only once energy is exhausted.</summary>
        public bool CanFillEnergy => Energy <= 0;

        /// <summary>Restores energy.</summary>
        /// <exception cref="InvalidOperationException">Energy is not yet empty.</exception>
        public void FillEnergy()
        {
            if (!CanFillEnergy)
            {
                throw new InvalidOperationException($"Cannot refill: {Energy} energy remains.");
            }

            Energy = EnergyPerRefill;
        }

        /// <summary>Whether enough experience has been earned to level up.</summary>
        public bool CanLevelUp => Experience >= ExperienceToLevelUp;

        /// <summary>
        /// Spends the level-up threshold and advances a level, carrying any surplus.
        /// </summary>
        /// <exception cref="InvalidOperationException">Not enough experience.</exception>
        public void LevelUp()
        {
            if (!CanLevelUp)
            {
                throw new InvalidOperationException(
                    $"Cannot level up: {Experience}/{ExperienceToLevelUp} experience.");
            }

            Experience -= ExperienceToLevelUp;
            Level++;
        }

        private int FirstEmptyCell()
        {
            for (int index = 0; index < CellCount; index++)
            {
                if (_tiers[index] == Empty)
                {
                    return index;
                }
            }

            return -1;
        }

        private static void RequireInRange(int index, string parameterName)
        {
            if (index < 0 || index >= CellCount)
            {
                throw new ArgumentOutOfRangeException(parameterName, index, $"Cells are 0..{CellCount - 1}.");
            }
        }
    }
}
