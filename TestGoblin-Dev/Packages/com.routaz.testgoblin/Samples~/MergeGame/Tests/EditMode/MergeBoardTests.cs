using System;
using NUnit.Framework;
using DemoGame.Merge;

namespace DemoGame.Tests
{
    /// <summary>
    /// The merge game's rules, tested without Unity and without TestGoblin.
    /// </summary>
    /// <remarks>
    /// Deliberately independent of the framework. TestGoblin is verified by driving this
    /// game, so if the game's correctness rested on TestGoblin the argument would be
    /// circular — each proving the other.
    /// </remarks>
    public class MergeBoardTests
    {
        [Test]
        public void Spawn_AlwaysProducesTheSameTier_InTheLowestEmptyCell()
        {
            var board = new MergeBoard();

            Assert.AreEqual(0, board.Spawn());
            Assert.AreEqual(1, board.Spawn());
            Assert.AreEqual(MergeBoard.SpawnTier, board.TierAt(0));
            Assert.AreEqual(MergeBoard.SpawnTier, board.TierAt(1));
        }

        [Test]
        public void Spawn_SpendsEnergy_AndStopsWhenEmpty()
        {
            var board = new MergeBoard(startingEnergy: 2);

            board.Spawn();
            board.Spawn();

            Assert.AreEqual(0, board.Energy);
            Assert.IsFalse(board.CanSpawn);
            Assert.Throws<InvalidOperationException>(() => board.Spawn());
        }

        [Test]
        public void Merge_PromotesDestination_EmptiesSource_AndGrantsFlatExperience()
        {
            var board = new MergeBoard(experiencePerMerge: 10);
            board.Spawn();
            board.Spawn();

            board.Merge(1, 0);

            Assert.AreEqual(2, board.TierAt(0), "Destination should be promoted a tier.");
            Assert.AreEqual(MergeBoard.Empty, board.TierAt(1), "Source should be emptied.");
            Assert.AreEqual(10, board.Experience);
            Assert.AreEqual(1, board.ItemCount);
        }

        [Test]
        public void Merge_RequiresMatchingTiers()
        {
            var board = new MergeBoard();
            board.Spawn();
            board.Spawn();
            board.Merge(1, 0);   // cell 0 is now tier 2
            board.Spawn();       // cell 1 is tier 1 again

            Assert.IsFalse(board.CanMerge(1, 0));
            Assert.Throws<InvalidOperationException>(() => board.Merge(1, 0));
        }

        [Test]
        public void MergingEverything_Terminates_BecauseEachMergeRemovesAnItem()
        {
            // The property that lets "merge while possible" be a safe keyword: item count
            // strictly decreases, so the loop cannot run forever.
            var board = new MergeBoard(startingEnergy: 6);

            for (int i = 0; i < 6; i++)
            {
                board.Spawn();
            }

            int previousCount = board.ItemCount;
            int merges = 0;

            while (board.FindMergeablePair() is { } pair)
            {
                board.Merge(pair.From, pair.To);
                merges++;

                Assert.Less(board.ItemCount, previousCount, "Every merge must remove exactly one item.");
                previousCount = board.ItemCount;

                Assert.Less(merges, MergeBoard.CellCount, "Merging must terminate.");
            }

            // 6 tier-1 -> 3 merges -> 3 tier-2; then one tier-2 pair merges. 4 merges total.
            Assert.AreEqual(4, merges);
            Assert.AreEqual(40, board.Experience);
        }

        [Test]
        public void FillEnergy_IsOfferedOnlyWhenEnergyIsExhausted()
        {
            var board = new MergeBoard(startingEnergy: 1, energyPerRefill: 6);

            Assert.IsFalse(board.CanFillEnergy);
            Assert.Throws<InvalidOperationException>(() => board.FillEnergy());

            board.Spawn();

            Assert.IsTrue(board.CanFillEnergy);
            board.FillEnergy();
            Assert.AreEqual(6, board.Energy);
        }

        [Test]
        public void LevelUp_IsOfferedOnlyAtTheThreshold_AndCarriesSurplus()
        {
            var board = new MergeBoard(startingEnergy: 6, experiencePerMerge: 30, experienceToLevelUp: 50);

            board.Spawn();
            board.Spawn();
            Assert.IsFalse(board.CanLevelUp);
            Assert.Throws<InvalidOperationException>(() => board.LevelUp());

            board.Merge(1, 0);   // 30
            board.Spawn();
            board.Spawn();
            board.Merge(2, 1);   // 60

            Assert.IsTrue(board.CanLevelUp);
            board.LevelUp();

            Assert.AreEqual(2, board.Level);
            Assert.AreEqual(10, board.Experience, "Surplus experience should carry to the next level.");
        }

        [Test]
        public void OneEnergyBarIsNotEnoughToLevel_WhichIsWhatMakesRefillNecessary()
        {
            // Pins the intended shape of the demo loop: spawn, merge, run dry, refill,
            // continue. If this ever passes on a single bar the scenario stops
            // exercising the refill path.
            var board = new MergeBoard();

            while (board.CanSpawn)
            {
                board.Spawn();
            }

            while (board.FindMergeablePair() is { } pair)
            {
                board.Merge(pair.From, pair.To);
            }

            Assert.AreEqual(40, board.Experience);
            Assert.IsFalse(board.CanLevelUp, "A single energy bar should fall short of a level.");
            Assert.IsTrue(board.CanFillEnergy);
        }
    }
}
