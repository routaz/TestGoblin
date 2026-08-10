using NUnit.Framework;
using Routaz.TestGoblin.Runtime.Scenarios;

namespace DemoGame.Tests
{
    /// <summary>
    /// Guards the keyword names this sample publishes.
    /// </summary>
    /// <remarks>
    /// A `[ScenarioAction]` name is a contract with every saved scenario that uses it, so
    /// renaming one silently breaks files on disk that no compiler will check. This test is
    /// what turns that into a build failure instead.
    /// <para>
    /// It lives with the sample rather than with the framework: these names belong to this
    /// game, and TestGoblin has no opinion about them.
    /// </para>
    /// </remarks>
    public class MergeGameVocabularyTests
    {
        [TestCase("LogIn")]
        [TestCase("Spawn")]
        [TestCase("SpawnUntilEnergyEmpty")]
        [TestCase("MergeAllPossible")]
        [TestCase("FillEnergy")]
        [TestCase("LevelUp")]
        [TestCase("PlayUntilLevelUpReady")]
        public void Vocabulary_ContainsKeyword(string keyword)
        {
            var registry = KeywordRegistry.FromLoadedAssemblies();

            Assert.IsTrue(registry.Contains(keyword),
                $"Scenarios reference '{keyword}' by name; renaming it breaks them silently.");
        }

        [Test]
        public void Vocabulary_HasNoMalformedActions()
        {
            var registry = KeywordRegistry.FromLoadedAssemblies();

            CollectionAssert.IsEmpty(registry.Problems,
                "A malformed [ScenarioAction] is invisible until someone tries to use it.");
        }
    }
}
