using System;
using NUnit.Framework;
using Routaz.TestGoblin.Runtime.Scenarios;

namespace DemoGame.Vault.Tests
{
    /// <summary>The vault's rules, tested without Unity and without TestGoblin.</summary>
    public class VaultStateTests
    {
        [Test]
        public void TakingTheKey_IsAllowedByTheRulesEvenBeforeTheLever()
        {
            // Pins the design the occlusion test depends on: the GAME does not forbid this.
            // If a rule were added here, the wall would stop being what prevents the click,
            // and the 3D driver's reachability check would no longer be under test.
            var state = new VaultState();

            Assert.IsTrue(state.CanTakeKey, "Reachability is physics, not a game rule.");
        }

        [Test]
        public void TheGate_NeedsTheKey()
        {
            var state = new VaultState();

            Assert.IsFalse(state.CanUnlockGate);
            Assert.Throws<InvalidOperationException>(() => state.UnlockGate());

            state.TakeKey();

            Assert.IsTrue(state.CanUnlockGate);
            state.UnlockGate();
            Assert.IsTrue(state.IsGateOpen);
        }

        [Test]
        public void TheGate_CannotBeOpenedTwice()
        {
            var state = new VaultState();
            state.TakeKey();
            state.UnlockGate();

            Assert.IsFalse(state.CanUnlockGate);
        }

        [Test]
        public void TheKey_CanOnlyBeTakenOnce()
        {
            var state = new VaultState();
            state.TakeKey();

            Assert.IsFalse(state.CanTakeKey);
            Assert.Throws<InvalidOperationException>(() => state.TakeKey());
        }
    }

    /// <summary>Guards the keyword names this sample publishes.</summary>
    public class VaultVocabularyTests
    {
        [TestCase("PullLever")]
        [TestCase("TakeKey")]
        [TestCase("UnlockGate")]
        [TestCase("OpenInventory")]
        [TestCase("CloseInventory")]
        [TestCase("AssertCarryingKey")]
        [TestCase("AssertGateState")]
        public void Vocabulary_ContainsKeyword(string keyword)
        {
            var registry = KeywordRegistry.FromLoadedAssemblies();

            Assert.IsTrue(registry.Contains(keyword),
                $"Scenarios reference '{keyword}' by name; renaming it breaks them silently.");
        }

        [Test]
        public void Vocabulary_DoesNotClashWithOtherSamples()
        {
            var registry = KeywordRegistry.FromLoadedAssemblies();

            CollectionAssert.IsEmpty(registry.Problems,
                "A duplicate keyword name means one game's vocabulary is being silently dropped.");
        }
    }
}
