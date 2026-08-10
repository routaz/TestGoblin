using System.Threading.Tasks;
using Routaz.TestGoblin.Runtime.Driver;
using Routaz.TestGoblin.Runtime.Screens;

namespace DemoGame.Adventure.Screens
{
    /// <summary>
    /// The point-and-click demo's screens: login, loading, and the room.
    /// </summary>
    /// <remarks>
    /// Object names are duplicated here rather than referenced from <c>AdventureView</c>,
    /// deliberately — a device tier drives a built player from another process and cannot see
    /// the game's types. Drift surfaces as "no active object with that name", which is honest.
    /// </remarks>
    public sealed class AdventureScreen : ScreenBase
    {
        private const string LoginButtonName = "LoginButton";
        private const string LoadingScreenName = "LoadingScreen";
        private const string RoomScreenName = "RoomScreen";
        private const string HotspotNameFormat = "Hotspot_{0}";
        private const string InventorySlotNameFormat = "InventorySlot_{0}";
        private const string DoorStateLabelName = "DoorStateLabel";

        private const int ProgressPollIntervalMs = 50;

        protected override string SignatureObjectName => LoginButtonName;

        public AdventureScreen(IGameDriver driver) : base(driver)
        {
        }

        /// <summary>Clicks Log in, which starts the load.</summary>
        public async Task TapLogIn()
        {
            GoblinObject button = await Driver.WaitForObject(LoginButtonName);
            await Driver.Tap(button);
        }

        /// <summary>Whether the loading screen is on screen right now.</summary>
        public async Task<bool> IsLoading()
        {
            return await Driver.FindObject(LoadingScreenName) != null;
        }

        /// <summary>Whether the room is on screen right now.</summary>
        public async Task<bool> IsRoomVisible()
        {
            return await Driver.FindObject(RoomScreenName) != null;
        }

        /// <summary>
        /// Waits out the load, failing if it takes longer than <paramref name="maxSeconds"/>.
        /// </summary>
        /// <remarks>
        /// The budget is the wait's own timeout rather than a separate stopwatch, so the
        /// limit and the polling are one thing and blowing it is an ordinary timeout.
        /// Requires the loading screen to be gone as well as the room present — waiting only
        /// for the room would pass while a loading overlay still covered it.
        /// </remarks>
        public Task WaitUntilRoomReady(float maxSeconds)
        {
            return Driver.WaitForCondition(
                async () => await Driver.FindObject(LoadingScreenName) == null
                            && await Driver.FindObject(RoomScreenName) != null,
                maxSeconds,
                ProgressPollIntervalMs,
                $"the room to finish loading within {maxSeconds}s");
        }

        /// <summary>Clicks a hotspot in the room.</summary>
        public async Task ClickHotspot(string thing)
        {
            GoblinObject hotspot = await Driver.WaitForObject(string.Format(HotspotNameFormat, thing));
            await Driver.Tap(hotspot);
        }

        /// <summary>Clicks an inventory slot, which selects the item for use.</summary>
        public async Task SelectInventoryItem(string item)
        {
            GoblinObject slot = await Driver.WaitForObject(string.Format(InventorySlotNameFormat, item));
            await Driver.Tap(slot);
        }

        /// <summary>Whether the item is in the inventory, judged by its slot being on screen.</summary>
        public async Task<bool> IsCarrying(string item)
        {
            return await Driver.FindObject(string.Format(InventorySlotNameFormat, item)) != null;
        }

        /// <summary>Whether the thing is still in the room to interact with.</summary>
        public async Task<bool> IsInRoom(string thing)
        {
            return await Driver.FindObject(string.Format(HotspotNameFormat, thing)) != null;
        }

        /// <summary>The door's state as the game reports it: "Open" or "Closed".</summary>
        public async Task<string> GetDoorState()
        {
            GoblinObject label = await Driver.WaitForObject(DoorStateLabelName);
            return await Driver.GetText(label);
        }
    }
}
