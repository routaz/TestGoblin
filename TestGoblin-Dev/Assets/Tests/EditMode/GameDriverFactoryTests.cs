using System;
using NUnit.Framework;
using Routaz.TestGoblin.Runtime.Config;
using Routaz.TestGoblin.Runtime.Driver;

namespace Routaz.TestGoblin.Tests
{
    /// <summary>
    /// Covers the config-to-driver mapping — the mechanism behind the claim that
    /// switching execution tiers changes configuration and never code.
    /// </summary>
    public class GameDriverFactoryTests
    {
        private static TestRunConfig ConfigFor(
            ExecutionTarget target, InputBackend backend = InputBackend.UGui)
        {
            return new TestRunConfig
            {
                ExecutionTarget = target,
                InputBackend = backend,
                DefaultTimeout = 5f,
                DefaultPollInterval = 100,
                DriverHost = "127.0.0.1",
                DriverPort = 13000
            };
        }

        [Test]
        public void Fake_GivesTheInMemoryDriver()
        {
            Assert.IsInstanceOf<FakeGameDriver>(GameDriverFactory.Create(ConfigFor(ExecutionTarget.Fake)));
        }

        [Test]
        public void Editor_DefaultsToTheUGuiDriver()
        {
            Assert.IsInstanceOf<InProcessGameDriver>(GameDriverFactory.Create(ConfigFor(ExecutionTarget.Editor)));
        }

        [Test]
        public void Editor_WithPhysicsBackend_GivesThePhysicsDriver()
        {
            Assert.IsInstanceOf<PhysicsGameDriver>(
                GameDriverFactory.Create(ConfigFor(ExecutionTarget.Editor, InputBackend.Physics2D)));
        }

        [Test]
        public void Editor_With3DBackend_GivesThe3DDriver()
        {
            Assert.IsInstanceOf<Physics3DGameDriver>(
                GameDriverFactory.Create(ConfigFor(ExecutionTarget.Editor, InputBackend.Physics3D)));
        }

        [Test]
        public void Editor_WithUiToolkitBackend_GivesTheUiToolkitDriver()
        {
            Assert.IsInstanceOf<UiToolkitGameDriver>(
                GameDriverFactory.Create(ConfigFor(ExecutionTarget.Editor, InputBackend.UiToolkit)));
        }

        [Test]
        public void InputBackend_IsIgnoredForTargetsThatCannotUseIt()
        {
            // The fake has nothing to hit and out-of-process drivers hit-test inside the
            // running game, so the setting is meaningless for both — and must not change
            // which driver they get.
            Assert.IsInstanceOf<FakeGameDriver>(
                GameDriverFactory.Create(ConfigFor(ExecutionTarget.Fake, InputBackend.Physics2D)));

            Assert.IsInstanceOf<AltTesterGameDriver>(
                GameDriverFactory.Create(ConfigFor(ExecutionTarget.Device, InputBackend.Physics2D)));
        }

        [Test]
        public void CreateLayered_WithOneBackend_IsJustThatDriver()
        {
            IGameDriver driver = GameDriverFactory.CreateLayered(
                ConfigFor(ExecutionTarget.Editor), new[] { InputBackend.Physics3D });

            Assert.IsInstanceOf<Physics3DGameDriver>(driver,
                "A single layer should not be wrapped in a composite for nothing.");
        }

        [Test]
        public void CreateLayered_WithSeveralBackends_GivesAComposite()
        {
            IGameDriver driver = GameDriverFactory.CreateLayered(
                ConfigFor(ExecutionTarget.Editor),
                new[] { InputBackend.UiToolkit, InputBackend.Physics3D });

            Assert.IsInstanceOf<CompositeGameDriver>(driver);
        }

        [Test]
        public void CreateLayered_WithNoBackends_FallsBackToTheConfig()
        {
            IGameDriver driver = GameDriverFactory.CreateLayered(
                ConfigFor(ExecutionTarget.Editor, InputBackend.Physics2D), Array.Empty<InputBackend>());

            Assert.IsInstanceOf<PhysicsGameDriver>(driver);
        }

        [Test]
        public void CreateLayered_OnATargetThatCannotLayer_Throws()
        {
            // Layering is an in-process concept: the fake has nothing to draw, and an
            // out-of-process driver hit-tests inside the running game where layering is the
            // game's own business. Asking for it there is a mistake, not a no-op.
            Assert.Throws<InvalidOperationException>(() => GameDriverFactory.CreateLayered(
                ConfigFor(ExecutionTarget.Device),
                new[] { InputBackend.UiToolkit, InputBackend.Physics3D }));
        }

        [Test]
        public void UnmappedInputBackend_Throws()
        {
            // Guards the inner switch: adding an InputBackend without a driver must fail
            // loudly rather than silently falling through to uGUI.
            Assert.Throws<ArgumentOutOfRangeException>(
                () => GameDriverFactory.Create(ConfigFor(ExecutionTarget.Editor, (InputBackend)999)));
        }

        [Test]
        public void OutOfProcessTargets_GiveTheAltTesterDriver()
        {
            // Construction must succeed even without the SDK: the driver only throws when
            // used, so a missing tier fails at the point of use with a clear message rather
            // than at wiring time with an obscure one.
            Assert.IsInstanceOf<AltTesterGameDriver>(
                GameDriverFactory.Create(ConfigFor(ExecutionTarget.StandalonePlayer)));

            Assert.IsInstanceOf<AltTesterGameDriver>(
                GameDriverFactory.Create(ConfigFor(ExecutionTarget.Device)));
        }

        [Test]
        public void UnmappedTarget_Throws()
        {
            // Guards the switch: adding an ExecutionTarget without a mapping must fail
            // loudly here rather than returning null for something to dereference later.
            var config = new TestRunConfig { ExecutionTarget = (ExecutionTarget)999 };

            Assert.Throws<ArgumentOutOfRangeException>(() => GameDriverFactory.Create(config));
        }

        [Test]
        public void NullConfig_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => GameDriverFactory.Create(null));
        }
    }
}
