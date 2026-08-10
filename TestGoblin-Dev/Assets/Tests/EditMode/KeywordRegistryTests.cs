using System;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Routaz.TestGoblin.Runtime.Scenarios;

namespace Routaz.TestGoblin.Tests
{
    /// <summary>
    /// Covers the keyword vocabulary: what the registry exposes, and — mostly — what it
    /// refuses. The rejections matter more than the happy path, because each one is a
    /// mistake that would otherwise surface when a QA engineer runs a scenario.
    /// </summary>
    public class KeywordRegistryTests
    {
        public enum Difficulty
        {
            Easy,
            Hard
        }

        private class WellFormedActions
        {
            [ScenarioAction("DoThing", Description = "Does the thing.")]
            public Task DoThing()
            {
                return Task.CompletedTask;
            }

            [ScenarioAction("DoThingWithArgs")]
            public Task DoThingWithArgs(int count, string label, bool loudly = true, Difficulty difficulty = Difficulty.Hard)
            {
                return Task.CompletedTask;
            }

            /// <summary>Untagged: must never appear in the vocabulary.</summary>
            public Task HelperNobodyAskedFor()
            {
                return Task.CompletedTask;
            }
        }

        private class DuplicateNameActions
        {
            [ScenarioAction("DoThing")]
            public Task AlsoDoThing()
            {
                return Task.CompletedTask;
            }
        }

        private class NonTaskAction
        {
            [ScenarioAction("NotATask")]
            public void NotATask()
            {
            }
        }

        private class UnsupportedParameterAction
        {
            [ScenarioAction("TakesAnObject")]
            public Task TakesAnObject(object anything)
            {
                return Task.CompletedTask;
            }
        }

        [Test]
        public void Registry_FindsTaggedMethods_AndIgnoresUntaggedOnes()
        {
            var registry = new KeywordRegistry(new[] { typeof(WellFormedActions) });

            CollectionAssert.AreEqual(
                new[] { "DoThing", "DoThingWithArgs" },
                registry.Keywords.Select(keyword => keyword.Name).ToArray());

            Assert.IsFalse(registry.Contains("HelperNobodyAskedFor"),
                "Untagged methods must not leak into the vocabulary.");
        }

        [Test]
        public void Registry_ExposesDescription()
        {
            var registry = new KeywordRegistry(new[] { typeof(WellFormedActions) });

            Assert.AreEqual("Does the thing.", registry.Resolve("DoThing").Description);
        }

        [Test]
        public void Registry_DescribesParameters_WithTypesAndDefaults()
        {
            var registry = new KeywordRegistry(new[] { typeof(WellFormedActions) });
            Keyword keyword = registry.Resolve("DoThingWithArgs");

            Assert.AreEqual(4, keyword.Parameters.Count);

            KeywordParameter count = keyword.Parameters[0];
            Assert.AreEqual("count", count.Name);
            Assert.AreEqual("int", count.TypeName);
            Assert.IsFalse(count.IsOptional, "A parameter with no default is required.");

            KeywordParameter loudly = keyword.Parameters[2];
            Assert.AreEqual("bool", loudly.TypeName);
            Assert.IsTrue(loudly.IsOptional);
            Assert.AreEqual(true, loudly.DefaultValue);
        }

        [Test]
        public void Registry_ExposesEnumValues_SoTheUiCanOfferAChoice()
        {
            var registry = new KeywordRegistry(new[] { typeof(WellFormedActions) });
            KeywordParameter difficulty = registry.Resolve("DoThingWithArgs").Parameters[3];

            Assert.AreEqual("enum", difficulty.TypeName);
            CollectionAssert.AreEqual(new[] { "Easy", "Hard" }, difficulty.AllowedValues.ToArray());
            Assert.AreEqual(Difficulty.Hard, difficulty.DefaultValue);
        }

        [Test]
        public void Registry_DuplicateKeywordName_Throws()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new KeywordRegistry(new[] { typeof(WellFormedActions), typeof(DuplicateNameActions) }));

            // Both offenders must be named — knowing only that a clash exists is useless.
            StringAssert.Contains("DoThing", exception.Message);
            StringAssert.Contains("AlsoDoThing", exception.Message);
        }

        [Test]
        public void Registry_ActionNotReturningTask_Throws()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new KeywordRegistry(new[] { typeof(NonTaskAction) }));

            StringAssert.Contains("must return Task", exception.Message);
        }

        [Test]
        public void Registry_UnsupportedParameterType_Throws()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => new KeywordRegistry(new[] { typeof(UnsupportedParameterAction) }));

            StringAssert.Contains("anything", exception.Message);
        }

        [Test]
        public void Resolve_UnknownKeyword_ListsWhatIsAvailable()
        {
            var registry = new KeywordRegistry(new[] { typeof(WellFormedActions) });

            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => registry.Resolve("CompletTutorial"));

            // A typo is the likely cause, so the message has to show the real names.
            StringAssert.Contains("DoThing", exception.Message);
        }

        [Test]
        public void FromLoadedAssemblies_SkipsMalformedActions_WithoutLosingValidOnes()
        {
            // Scanning everything, including this test assembly and its deliberately
            // malformed fixtures. A domain-wide sweep MUST tolerate them — any real project
            // that tests its own keywords has the same shape — while reporting what it skipped.
            var registry = KeywordRegistry.FromLoadedAssemblies(_ => true);

            Assert.IsTrue(registry.Contains("DoThing"),
                "A malformed action elsewhere must not cost the whole vocabulary.");

            Assert.IsNotEmpty(registry.Problems,
                "Skipped actions must be reported, not silently swallowed.");

            StringAssert.Contains("NotATask", string.Join(" ", registry.Problems));
        }

        [Test]
        public void FromLoadedAssemblies_ByDefault_ExcludesTestFixtures()
        {
            // The catalog QA sees must contain the game's vocabulary and nothing else.
            // Without this, the fixtures above ship as real keywords in the scenario builder.
            //
            // Asserts only on what must be ABSENT, deliberately: this project ships no game,
            // so requiring a specific production keyword here would couple the framework's
            // tests to whichever sample happens to be imported.
            var registry = KeywordRegistry.FromLoadedAssemblies();

            Assert.IsFalse(registry.Contains("DoThing"),
                "Test fixtures must not appear in the vocabulary offered to QA.");

            Assert.IsEmpty(registry.Problems,
                "Problems from test fixtures must not pollute the production catalog.");
        }
    }
}
