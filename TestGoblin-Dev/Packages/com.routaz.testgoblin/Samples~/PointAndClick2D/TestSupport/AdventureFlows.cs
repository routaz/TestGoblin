using System;
using System.Threading.Tasks;
using DemoGame.Adventure.Screens;
using Routaz.TestGoblin.Runtime.Driver;
using Routaz.TestGoblin.Runtime.Scenarios;

namespace DemoGame.Adventure.Flows
{
    /// <summary>
    /// The point-and-click demo's keyword vocabulary.
    /// </summary>
    /// <remarks>
    /// Keyword names live in a <em>global</em> namespace — the registry scans every loaded
    /// assembly — so these deliberately avoid the merge sample's names. In a real project
    /// that holds one game the question never arises, but two samples in one project makes
    /// it concrete: a clash would be reported and one of them silently ignored.
    /// </remarks>
    public sealed class AdventureFlows
    {
        private readonly AdventureScreen _screen;

        /// <summary>The constructor shape <c>ScenarioExecutor</c> requires.</summary>
        public AdventureFlows(IGameDriver driver)
        {
            _screen = new AdventureScreen(driver);
        }

        [ScenarioAction("SignIn",
            Description = "Signs in and waits for the room, failing if loading exceeds the budget.")]
        public async Task SignIn(float maxLoadingSeconds = 5f)
        {
            await _screen.WaitUntilVisible();
            await _screen.TapLogIn();
            await _screen.WaitUntilRoomReady(maxLoadingSeconds);
        }

        [ScenarioAction("PickUp",
            Description = "Clicks a thing in the room and checks it reached the inventory.")]
        public async Task PickUp(string item)
        {
            if (!await _screen.IsInRoom(item))
            {
                throw new InvalidOperationException($"'{item}' is not in the room to pick up.");
            }

            await _screen.ClickHotspot(item);

            // The keyword checks its own outcome. One that merely clicked would pass against
            // a game where picking things up had stopped working.
            if (!await _screen.IsCarrying(item))
            {
                throw new InvalidOperationException($"Clicked '{item}' but it never reached the inventory.");
            }
        }

        [ScenarioAction("UseItemOn",
            Description = "Selects a carried item and uses it on something in the room.")]
        public async Task UseItemOn(string item, string target)
        {
            if (!await _screen.IsCarrying(item))
            {
                throw new InvalidOperationException($"Cannot use '{item}' — it is not being carried.");
            }

            await _screen.SelectInventoryItem(item);
            await _screen.ClickHotspot(target);
        }

        [ScenarioAction("AssertDoorState",
            Description = "Fails unless the door reads as the expected state, e.g. Open or Closed.")]
        public async Task AssertDoorState(string expected)
        {
            string actual = await _screen.GetDoorState();

            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Door is '{actual}', expected '{expected}'.");
            }
        }
    }
}
