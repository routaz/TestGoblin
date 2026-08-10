using System;
using System.Threading.Tasks;
using Routaz.TestGoblin.Runtime.Driver;
using Routaz.TestGoblin.Runtime.Scenarios;
using Routaz.TestGoblin.Runtime.Screens;

namespace DemoGame.Vault.Screens
{
    /// <summary>
    /// The vault's screen: every locator it uses, and nothing else.
    /// </summary>
    public sealed class VaultScreen : ScreenBase
    {
        private const string LeverName = "Hotspot_Lever";
        private const string KeyName = "Hotspot_Key";
        private const string GateName = "Hotspot_Gate";
        private const string GateStateLabelName = "GateStateLabel";
        private const string InventoryButtonName = "InventoryButton";
        private const string InventoryPanelName = "InventoryPanel";
        private const string KeySlotName = "KeySlot";

        protected override string SignatureObjectName => LeverName;

        public VaultScreen(IGameDriver driver) : base(driver)
        {
        }

        public async Task ClickLever()
        {
            GoblinObject lever = await Driver.WaitForObject(LeverName);
            await Driver.Tap(lever);
        }

        public async Task ClickKey()
        {
            GoblinObject key = await Driver.WaitForObject(KeyName);
            await Driver.Tap(key);
        }

        public async Task ClickGate()
        {
            GoblinObject gate = await Driver.WaitForObject(GateName);
            await Driver.Tap(gate);
        }

        /// <summary>Whether the key is still in the room, judged by its presence.</summary>
        public async Task<bool> IsKeyStillThere()
        {
            return await Driver.FindObject(KeyName) != null;
        }

        /// <summary>The gate's state as the game reports it: "Locked" or "Open".</summary>
        public async Task<string> GetGateState()
        {
            GoblinObject label = await Driver.WaitForObject(GateStateLabelName);
            return await Driver.GetText(label);
        }

        /// <summary>
        /// Clicks the HUD's inventory button, which is a UI Toolkit element rather than a mesh.
        /// </summary>
        /// <remarks>
        /// Reaching this needs a driver that spans both technologies — the world is meshes,
        /// this is UI Toolkit, and neither driver alone can see the other's objects.
        /// </remarks>
        public async Task ToggleInventory()
        {
            GoblinObject button = await Driver.WaitForObject(InventoryButtonName);
            await Driver.Tap(button);
        }

        /// <summary>Whether the inventory overlay is currently open.</summary>
        public async Task<bool> IsInventoryOpen()
        {
            return await Driver.FindObject(InventoryPanelName) != null;
        }

        /// <summary>Whether the inventory is showing the key.</summary>
        public async Task<bool> IsKeyInInventory()
        {
            return await Driver.FindObject(KeySlotName) != null;
        }
    }
}

namespace DemoGame.Vault.Flows
{
    using DemoGame.Vault.Screens;

    /// <summary>
    /// The vault's keyword vocabulary.
    /// </summary>
    /// <remarks>
    /// Names avoid the other samples' — keywords share one global namespace, since the
    /// registry scans every loaded assembly, and a clash would leave one game's vocabulary
    /// silently ignored. A real project holds one game and never faces this.
    /// </remarks>
    public sealed class VaultFlows
    {
        private readonly VaultScreen _screen;

        /// <summary>The constructor shape <c>ScenarioExecutor</c> requires.</summary>
        public VaultFlows(IGameDriver driver)
        {
            _screen = new VaultScreen(driver);
        }

        [ScenarioAction("PullLever",
            Description = "Pulls the lever, which retracts the wall hiding the key.")]
        public async Task PullLever()
        {
            await _screen.WaitUntilVisible();
            await _screen.ClickLever();
        }

        [ScenarioAction("TakeKey",
            Description = "Takes the key. Fails while the wall is still in front of it.")]
        public async Task TakeKey()
        {
            await _screen.ClickKey();

            // The keyword checks its own outcome. One that merely clicked would pass against
            // a game where taking things had stopped working.
            if (await _screen.IsKeyStillThere())
            {
                throw new InvalidOperationException("Clicked the key but it is still in the room.");
            }
        }

        [ScenarioAction("UnlockGate",
            Description = "Opens the gate with the key that has been taken.")]
        public async Task UnlockGate()
        {
            await _screen.ClickGate();

            string state = await _screen.GetGateState();

            if (!string.Equals(state, "Open", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Clicked the gate but it reads '{state}'.");
            }
        }

        [ScenarioAction("OpenInventory",
            Description = "Opens the HUD inventory, which covers the world behind it.")]
        public async Task OpenInventory()
        {
            if (await _screen.IsInventoryOpen())
            {
                return;
            }

            await _screen.ToggleInventory();

            if (!await _screen.IsInventoryOpen())
            {
                throw new InvalidOperationException("Clicked the inventory button but the panel did not open.");
            }
        }

        [ScenarioAction("CloseInventory",
            Description = "Closes the HUD inventory so the world can be interacted with again.")]
        public async Task CloseInventory()
        {
            if (!await _screen.IsInventoryOpen())
            {
                return;
            }

            await _screen.ToggleInventory();

            if (await _screen.IsInventoryOpen())
            {
                throw new InvalidOperationException("Clicked the inventory button but the panel did not close.");
            }
        }

        [ScenarioAction("AssertCarryingKey",
            Description = "Fails unless the inventory is showing the key. Requires the inventory to be open.")]
        public async Task AssertCarryingKey()
        {
            if (!await _screen.IsInventoryOpen())
            {
                throw new InvalidOperationException("The inventory is closed, so its contents cannot be checked.");
            }

            if (!await _screen.IsKeyInInventory())
            {
                throw new InvalidOperationException("The inventory is open but the key is not in it.");
            }
        }

        [ScenarioAction("AssertGateState",
            Description = "Fails unless the gate reads as the expected state, e.g. Open or Locked.")]
        public async Task AssertGateState(string expected)
        {
            string actual = await _screen.GetGateState();

            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Gate is '{actual}', expected '{expected}'.");
            }
        }
    }
}
