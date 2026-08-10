using System;
using NUnit.Framework;
using Routaz.TestGoblin.Runtime.Scenarios;

namespace DemoGame.Adventure.Tests
{
    /// <summary>
    /// The point-and-click game's rules, tested without Unity and without TestGoblin.
    /// </summary>
    public class AdventureStateTests
    {
        [Test]
        public void PickUp_MovesTheKeyIntoTheInventory_AndOnlyOnce()
        {
            var state = new AdventureState();

            Assert.IsTrue(state.CanPickUp(AdventureState.Key));
            state.PickUp(AdventureState.Key);

            CollectionAssert.Contains(state.Inventory, AdventureState.Key);
            Assert.IsFalse(state.CanPickUp(AdventureState.Key), "It is no longer in the room.");
            Assert.Throws<InvalidOperationException>(() => state.PickUp(AdventureState.Key));
        }

        [Test]
        public void Select_RequiresCarryingTheItem()
        {
            var state = new AdventureState();

            Assert.Throws<InvalidOperationException>(() => state.Select(AdventureState.Key));

            state.PickUp(AdventureState.Key);
            state.Select(AdventureState.Key);

            Assert.AreEqual(AdventureState.Key, state.SelectedItem);
        }

        [Test]
        public void Door_NeedsTheKeySelected_NotMerelyCarried()
        {
            // The two-step interaction is the point: carrying the key is not enough, which is
            // what makes UseItemOn a real sequence rather than a single click.
            var state = new AdventureState();
            state.PickUp(AdventureState.Key);

            Assert.IsFalse(state.CanUseSelectedOn(AdventureState.Door),
                "Carrying the key must not be enough on its own.");

            state.Select(AdventureState.Key);

            Assert.IsTrue(state.CanUseSelectedOn(AdventureState.Door));
            state.UseSelectedOn(AdventureState.Door);

            Assert.IsTrue(state.IsDoorOpen);
        }

        [Test]
        public void Door_CannotBeOpenedTwice()
        {
            var state = new AdventureState();
            state.PickUp(AdventureState.Key);
            state.Select(AdventureState.Key);
            state.UseSelectedOn(AdventureState.Door);

            Assert.IsFalse(state.CanUseSelectedOn(AdventureState.Door));
        }
    }

    /// <summary>
    /// Guards the keyword names this sample publishes, since scenarios reference them by
    /// name and no compiler checks that.
    /// </summary>
    public class AdventureVocabularyTests
    {
        [TestCase("SignIn")]
        [TestCase("PickUp")]
        [TestCase("UseItemOn")]
        [TestCase("AssertDoorState")]
        public void Vocabulary_ContainsKeyword(string keyword)
        {
            var registry = KeywordRegistry.FromLoadedAssemblies();

            Assert.IsTrue(registry.Contains(keyword),
                $"Scenarios reference '{keyword}' by name; renaming it breaks them silently.");
        }

        [Test]
        public void Vocabulary_DoesNotClashWithOtherSamples()
        {
            // Keyword names are global across every loaded assembly. Two samples in one
            // project would otherwise shadow each other, and the loser is simply ignored.
            var registry = KeywordRegistry.FromLoadedAssemblies();

            CollectionAssert.IsEmpty(registry.Problems,
                "A duplicate keyword name means one game's vocabulary is being silently dropped.");
        }
    }
}
