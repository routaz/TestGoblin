using System;
using System.Collections.Generic;
using Routaz.TestGoblin.Runtime.Config;

namespace Routaz.TestGoblin.Runtime.Driver
{
    /// <summary>
    /// Builds the driver a <see cref="TestRunConfig"/> asks for.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what makes "switching targets changes config, never code" true rather than
    /// aspirational. It is the single place that decides which <see cref="IGameDriver"/> a
    /// tier gets, so nothing above the driver layer ever names an implementation.
    /// </para>
    /// <para>
    /// It lives in the framework rather than in a test base class on purpose: choosing a
    /// driver from configuration is framework logic, and a consuming project needs it in
    /// whatever fixtures it writes.
    /// </para>
    /// </remarks>
    public static class GameDriverFactory
    {
        /// <summary>
        /// Creates the driver for <paramref name="config"/>'s execution target.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The target has no driver mapping.</exception>
        public static IGameDriver Create(TestRunConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            switch (config.ExecutionTarget)
            {
                case ExecutionTarget.Fake:
                    // No game, no engine APIs — the only tier that works in edit mode,
                    // where there is no frame loop to pump a real driver's waits.
                    return new FakeGameDriver(config.DefaultTimeout, config.DefaultPollInterval);

                case ExecutionTarget.Editor:
                    // Play mode, driving the live scene in-process. Requires frames to be
                    // pumping, so fixtures using this must be [UnityTest]. Which driver
                    // depends on what the game's clickable things actually are.
                    return CreateInProcess(config);

                case ExecutionTarget.StandalonePlayer:
                case ExecutionTarget.Device:
                    // Out of process. Currently throws unless the AltTester SDK is present —
                    // deliberately, so an unavailable tier fails loudly rather than silently
                    // falling back to a driver that tests something else entirely.
                    return new AltTesterGameDriver(config);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(config),
                        config.ExecutionTarget,
                        "No driver mapping for this execution target.");
            }
        }

        /// <summary>
        /// Creates a driver for a stack of UI technologies, ordered topmost first.
        /// </summary>
        /// <param name="config">Timeouts and the execution target.</param>
        /// <param name="backends">
        /// The layers, topmost first — e.g. UI Toolkit over Physics3D. One entry behaves
        /// exactly like <see cref="Create"/>; none falls back to the config's own backend.
        /// </param>
        /// <remarks>
        /// A stack only means something in-process. The fake has nothing to draw and an
        /// out-of-process driver hit-tests inside the running game, where layering is the
        /// game's own business — so asking for layers on those targets is a mistake worth
        /// reporting rather than quietly ignoring.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Layers were asked for on a target that cannot use them.</exception>
        public static IGameDriver CreateLayered(TestRunConfig config, IReadOnlyList<InputBackend> backends)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (backends == null || backends.Count == 0)
            {
                return Create(config);
            }

            if (backends.Count == 1)
            {
                return Create(config with { InputBackend = backends[0] });
            }

            if (config.ExecutionTarget != ExecutionTarget.Editor)
            {
                throw new InvalidOperationException(
                    $"Execution target '{config.ExecutionTarget}' cannot use layered input backends " +
                    $"({string.Join(", ", backends)}). Layering is an in-process concept.");
            }

            var layers = new ILayeredGameDriver[backends.Count];

            for (int index = 0; index < backends.Count; index++)
            {
                IGameDriver driver = CreateInProcess(config with { InputBackend = backends[index] });

                if (driver is not ILayeredGameDriver layer)
                {
                    throw new InvalidOperationException(
                        $"Input backend '{backends[index]}' cannot take part in a layered stack.");
                }

                layers[index] = layer;
            }

            return new CompositeGameDriver(config.DefaultTimeout, config.DefaultPollInterval, layers);
        }

        /// <summary>
        /// Picks the in-process driver matching the game's UI technology.
        /// </summary>
        /// <remarks>
        /// Only in-process targets consult <see cref="InputBackend"/>. Out-of-process drivers
        /// do their own hit-testing inside the running game, and the fake has nothing to hit,
        /// so the setting is meaningless for both.
        /// </remarks>
        private static IGameDriver CreateInProcess(TestRunConfig config)
        {
            switch (config.InputBackend)
            {
                case InputBackend.UGui:
                    return new InProcessGameDriver(config.DefaultTimeout, config.DefaultPollInterval);

                case InputBackend.Physics2D:
                    return new PhysicsGameDriver(config.DefaultTimeout, config.DefaultPollInterval);

                case InputBackend.Physics3D:
                    return new Physics3DGameDriver(config.DefaultTimeout, config.DefaultPollInterval);

                case InputBackend.UiToolkit:
                    return new UiToolkitGameDriver(config.DefaultTimeout, config.DefaultPollInterval);

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(config),
                        config.InputBackend,
                        "No driver mapping for this input backend.");
            }
        }
    }
}
