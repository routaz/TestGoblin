using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using Routaz.TestGoblin.Runtime.Driver;
using Routaz.TestGoblin.Runtime.Scenarios;

namespace Routaz.TestGoblin.Tests
{
    /// <summary>
    /// Covers binding: turning a hand-written JSON step into a real method call.
    /// </summary>
    /// <remarks>
    /// Most of these assert on error messages. A scenario is authored by someone who cannot
    /// read the C#, so the message is the entire debugging experience — if it does not name
    /// the step and the argument, the feature does not work no matter what the code does.
    /// </remarks>
    public class ScenarioExecutorTests
    {
        public enum Speed
        {
            Slow,
            Fast
        }

        /// <summary>Records what was invoked, so tests can assert on the call itself.</summary>
        public class RecordingActions
        {
            public static readonly List<string> Calls = new List<string>();

            // The constructor convention the executor relies on.
            public RecordingActions(IGameDriver driver)
            {
            }

            [ScenarioAction("NoArgs")]
            public Task NoArgs()
            {
                Calls.Add("NoArgs");
                return Task.CompletedTask;
            }

            [ScenarioAction("WithArgs")]
            public Task WithArgs(int count, string label, bool loudly = false, Speed speed = Speed.Slow)
            {
                Calls.Add($"WithArgs({count},{label},{loudly},{speed})");
                return Task.CompletedTask;
            }

            [ScenarioAction("Explodes")]
            public Task Explodes()
            {
                throw new InvalidOperationException("boom");
            }
        }

        /// <summary>Has scenario actions but not the constructor the executor needs.</summary>
        public class NoDriverConstructorActions
        {
            [ScenarioAction("Unconstructable")]
            public Task Unconstructable()
            {
                return Task.CompletedTask;
            }
        }

        private ScenarioExecutor _executor;

        [SetUp]
        public void SetUp()
        {
            RecordingActions.Calls.Clear();

            var registry = new KeywordRegistry(new[] { typeof(RecordingActions) });
            _executor = new ScenarioExecutor(registry, new FakeGameDriver(1f, 10));
        }

        private static Scenario ScenarioWith(params ScenarioStep[] steps)
        {
            return new Scenario
            {
                Name = "test scenario",
                SourcePath = "memory",
                Steps = new List<ScenarioStep>(steps)
            };
        }

        private static ScenarioStep Step(string action, Dictionary<string, object> arguments = null)
        {
            return new ScenarioStep
            {
                Action = action,
                Arguments = arguments ?? new Dictionary<string, object>()
            };
        }

        [Test]
        public async Task Execute_RunsStepsInOrder()
        {
            await _executor.Execute(ScenarioWith(
                Step("NoArgs"),
                Step("WithArgs", new Dictionary<string, object> { { "count", 2L }, { "label", "x" } }),
                Step("NoArgs")));

            CollectionAssert.AreEqual(
                new[] { "NoArgs", "WithArgs(2,x,False,Slow)", "NoArgs" },
                RecordingActions.Calls);
        }

        [Test]
        public async Task Execute_UsesDefaults_ForOmittedOptionalArguments()
        {
            await _executor.Execute(ScenarioWith(
                Step("WithArgs", new Dictionary<string, object> { { "count", 1L }, { "label", "a" } })));

            CollectionAssert.Contains(RecordingActions.Calls, "WithArgs(1,a,False,Slow)");
        }

        [Test]
        public async Task Execute_ConvertsJsonScalarsToParameterTypes()
        {
            // JSON has no int type: Newtonsoft yields long, so binding must convert.
            await _executor.Execute(ScenarioWith(
                Step("WithArgs", new Dictionary<string, object>
                {
                    { "count", 7L },
                    { "label", "hello" },
                    { "loudly", true },
                    { "speed", "fast" }
                })));

            // Enum matching is case-insensitive: "fast" resolves to Speed.Fast.
            CollectionAssert.Contains(RecordingActions.Calls, "WithArgs(7,hello,True,Fast)");
        }

        [Test]
        public void Execute_UnknownAction_FailsBeforeRunningAnything()
        {
            // The bad step is LAST, yet nothing before it may run — the whole point of
            // binding up front is that a typo costs no game state.
            InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _executor.Execute(ScenarioWith(
                    Step("NoArgs"),
                    Step("Typo"))));

            CollectionAssert.IsEmpty(RecordingActions.Calls,
                "Binding must complete before any step executes.");

            StringAssert.Contains("step 2", exception.Message);
            StringAssert.Contains("NoArgs", exception.Message);
        }

        [Test]
        public void Execute_MissingRequiredArgument_NamesStepAndArgument()
        {
            InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _executor.Execute(ScenarioWith(
                    Step("WithArgs", new Dictionary<string, object> { { "count", 1L } }))));

            StringAssert.Contains("step 1", exception.Message);
            StringAssert.Contains("label", exception.Message);
        }

        [Test]
        public void Execute_UnknownArgument_IsRejectedRatherThanIgnored()
        {
            InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _executor.Execute(ScenarioWith(
                    Step("NoArgs", new Dictionary<string, object> { { "nonsense", 1L } }))));

            StringAssert.Contains("nonsense", exception.Message);
        }

        [Test]
        public void Execute_BadEnumValue_ListsTheLegalOnes()
        {
            InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _executor.Execute(ScenarioWith(
                    Step("WithArgs", new Dictionary<string, object>
                    {
                        { "count", 1L }, { "label", "a" }, { "speed", "Sideways" }
                    }))));

            StringAssert.Contains("Slow", exception.Message);
            StringAssert.Contains("Fast", exception.Message);
        }

        [Test]
        public void Execute_BadNumericValue_NamesTheArgument()
        {
            InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await _executor.Execute(ScenarioWith(
                    Step("WithArgs", new Dictionary<string, object>
                    {
                        { "count", "not-a-number" }, { "label", "a" }
                    }))));

            StringAssert.Contains("count", exception.Message);
        }

        [Test]
        public void Execute_StepThrows_ReportsScenarioAndStep()
        {
            ScenarioStepException exception = Assert.ThrowsAsync<ScenarioStepException>(
                async () => await _executor.Execute(ScenarioWith(Step("NoArgs"), Step("Explodes"))));

            StringAssert.Contains("step 2", exception.Message);
            StringAssert.Contains("Explodes", exception.Message);
            StringAssert.Contains("boom", exception.Message);
            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
        }

        [Test]
        public void Execute_DeclaringTypeWithoutDriverConstructor_ExplainsTheConvention()
        {
            var registry = new KeywordRegistry(new[] { typeof(NoDriverConstructorActions) });
            var executor = new ScenarioExecutor(registry, new FakeGameDriver(1f, 10));

            InvalidOperationException exception = Assert.ThrowsAsync<InvalidOperationException>(
                async () => await executor.Execute(ScenarioWith(Step("Unconstructable"))));

            StringAssert.Contains("IGameDriver", exception.Message);
        }

        [Test]
        public void Validate_ChecksEverything_WithoutRunningAnything()
        {
            _executor.Validate(ScenarioWith(
                Step("WithArgs", new Dictionary<string, object> { { "count", 1L }, { "label", "a" } })));

            CollectionAssert.IsEmpty(RecordingActions.Calls, "Validate must not execute steps.");
        }
    }
}
