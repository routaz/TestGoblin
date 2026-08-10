using System;

namespace DemoGame.Vault
{
    /// <summary>
    /// The vault's rules, with no Unity dependency.
    /// </summary>
    /// <remarks>
    /// Deliberately smaller than the 2D sample's state: this sample exists to demonstrate
    /// what 3D adds — <em>occlusion</em> — so it does not repeat the inventory-selection
    /// mechanic. Taking the key is enough to use it.
    /// </remarks>
    public sealed class VaultState
    {
        /// <summary>Whether the lever has retracted the wall.</summary>
        public bool IsWallOpen { get; private set; }

        /// <summary>Whether the key has been taken.</summary>
        public bool HasKey { get; private set; }

        /// <summary>Whether the gate has been unlocked.</summary>
        public bool IsGateOpen { get; private set; }

        /// <summary>Retracts the wall. Pulling a pulled lever does nothing.</summary>
        public void PullLever()
        {
            IsWallOpen = true;
        }

        /// <summary>
        /// Whether the key can be taken.
        /// </summary>
        /// <remarks>
        /// Note this says nothing about the wall being in the way. Reachability is a
        /// <em>physical</em> fact the driver establishes by raycast, not a rule the game
        /// enforces — which is exactly why the wall is worth having. A game that refused the
        /// click itself would prove nothing about the driver.
        /// </remarks>
        public bool CanTakeKey => !HasKey;

        /// <summary>Takes the key.</summary>
        /// <exception cref="InvalidOperationException">It has already been taken.</exception>
        public void TakeKey()
        {
            if (!CanTakeKey)
            {
                throw new InvalidOperationException("The key has already been taken.");
            }

            HasKey = true;
        }

        /// <summary>Whether the gate would open.</summary>
        public bool CanUnlockGate => HasKey && !IsGateOpen;

        /// <summary>Unlocks the gate.</summary>
        /// <exception cref="InvalidOperationException">No key, or already open.</exception>
        public void UnlockGate()
        {
            if (!CanUnlockGate)
            {
                throw new InvalidOperationException(
                    HasKey ? "The gate is already open." : "The gate needs the key.");
            }

            IsGateOpen = true;
        }
    }
}
