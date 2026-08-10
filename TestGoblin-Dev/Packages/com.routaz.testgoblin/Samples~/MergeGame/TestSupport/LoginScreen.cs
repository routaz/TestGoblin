using System.Threading.Tasks;
using Routaz.TestGoblin.Runtime.Driver;
using Routaz.TestGoblin.Runtime.Screens;

namespace DemoGame.Screens
{
    /// <summary>
    /// The login screen, and the journey from it through loading to the game.
    /// </summary>
    /// <remarks>
    /// The transition is the interesting part. Loading takes real time, so reaching the
    /// game means genuinely waiting for one screen to go and another to arrive — which is
    /// what the driver's polling waits exist for, and what every other screen in this demo
    /// is too instantaneous to exercise.
    /// </remarks>
    public sealed class LoginScreen : ScreenBase
    {
        private const string LoginButtonName = "LoginButton";
        private const string LoadingScreenName = "LoadingScreen";
        private const string LoadingLabelName = "LoadingLabel";
        private const string GameScreenName = "GameScreen";

        /// <summary>
        /// How often to re-check while loading. Fine-grained enough that the measured
        /// duration is a fair reflection of when the game actually appeared.
        /// </summary>
        private const int ProgressPollIntervalMs = 50;

        protected override string SignatureObjectName => LoginButtonName;

        public LoginScreen(IGameDriver driver) : base(driver)
        {
        }

        /// <summary>Taps Log in, which starts the load.</summary>
        public async Task TapLogIn()
        {
            GoblinObject button = await Driver.WaitForObject(LoginButtonName);
            await Driver.Tap(button);
        }

        /// <summary>Waits for the loading screen to appear.</summary>
        public Task WaitUntilLoading()
        {
            return Driver.WaitForObject(LoadingScreenName);
        }

        /// <summary>Whether the loading screen is on screen right now.</summary>
        public async Task<bool> IsLoading()
        {
            return await Driver.FindObject(LoadingScreenName) != null;
        }

        /// <summary>Whether the game screen is on screen right now.</summary>
        public async Task<bool> IsGameVisible()
        {
            return await Driver.FindObject(GameScreenName) != null;
        }

        /// <summary>The loading progress text, e.g. "42%".</summary>
        public async Task<string> GetLoadingProgress()
        {
            GoblinObject label = await Driver.WaitForObject(LoadingLabelName);
            return await Driver.GetText(label);
        }

        /// <summary>
        /// Waits out the load and returns once the game screen is up, failing if it takes
        /// longer than <paramref name="maxSeconds"/>.
        /// </summary>
        /// <param name="maxSeconds">The loading budget. Exceeding it fails the step.</param>
        /// <remarks>
        /// <para>
        /// The budget is passed as the wait's own timeout rather than measured separately
        /// with a stopwatch. A parallel timing mechanism would be a second source of truth
        /// about how long something took; using the framework's wait means the budget and
        /// the polling are the same thing, and blowing it produces a normal timeout that
        /// <c>ScenarioStepException</c> already knows how to report.
        /// </para>
        /// <para>
        /// The condition requires the loading screen to be gone <em>and</em> the game to be
        /// present. Waiting only for the game would pass while a loading overlay still
        /// covered it — the classic "the screen is technically there" false green.
        /// </para>
        /// </remarks>
        public Task WaitUntilGameReady(float maxSeconds)
        {
            return Driver.WaitForCondition(
                async () => await Driver.FindObject(LoadingScreenName) == null
                            && await Driver.FindObject(GameScreenName) != null,
                maxSeconds,
                ProgressPollIntervalMs,
                $"the game screen to finish loading within {maxSeconds}s");
        }
    }
}
