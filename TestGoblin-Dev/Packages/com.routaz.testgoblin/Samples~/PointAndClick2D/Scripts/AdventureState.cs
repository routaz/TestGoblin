using System;
using System.Collections.Generic;

namespace DemoGame.Adventure
{
    /// <summary>
    /// The point-and-click game's rules, with no Unity dependency.
    /// </summary>
    /// <remarks>
    /// Kept free of MonoBehaviour so the rules can be unit-tested without a scene and
    /// without TestGoblin. The framework is verified by driving this game, so the game's
    /// own correctness has to be established independently.
    /// </remarks>
    public sealed class AdventureState
    {
        /// <summary>The only item in this tiny world.</summary>
        public const string Key = "Key";

        /// <summary>The only thing worth using it on.</summary>
        public const string Door = "Door";

        private readonly HashSet<string> _inventory = new HashSet<string>(StringComparer.Ordinal);

        /// <summary>Items the player is carrying.</summary>
        public IReadOnlyCollection<string> Inventory => _inventory;

        /// <summary>The item currently held ready to use, or empty.</summary>
        public string SelectedItem { get; private set; } = "";

        /// <summary>Whether the door has been opened.</summary>
        public bool IsDoorOpen { get; private set; }

        /// <summary>Whether the player is carrying this item.</summary>
        public bool IsCarrying(string item)
        {
            return _inventory.Contains(item);
        }

        /// <summary>Whether the item is still in the room to be taken.</summary>
        public bool CanPickUp(string item)
        {
            return item == Key && !_inventory.Contains(item);
        }

        /// <summary>Takes an item into the inventory.</summary>
        /// <exception cref="InvalidOperationException">It is not there to take.</exception>
        public void PickUp(string item)
        {
            if (!CanPickUp(item))
            {
                throw new InvalidOperationException($"'{item}' cannot be picked up.");
            }

            _inventory.Add(item);
        }

        /// <summary>Holds an inventory item ready to use on something.</summary>
        /// <exception cref="InvalidOperationException">The player is not carrying it.</exception>
        public void Select(string item)
        {
            if (!_inventory.Contains(item))
            {
                throw new InvalidOperationException($"'{item}' is not in the inventory.");
            }

            SelectedItem = item;
        }

        /// <summary>
        /// Whether using the held item on the target would do anything.
        /// </summary>
        /// <remarks>
        /// The door needs the key <em>selected</em>, not merely carried — which is what makes
        /// this a two-step interaction and worth testing, rather than a single click.
        /// </remarks>
        public bool CanUseSelectedOn(string target)
        {
            return target == Door && SelectedItem == Key && !IsDoorOpen;
        }

        /// <summary>Uses the held item on a target.</summary>
        /// <exception cref="InvalidOperationException">The combination does nothing.</exception>
        public void UseSelectedOn(string target)
        {
            if (!CanUseSelectedOn(target))
            {
                throw new InvalidOperationException(
                    $"Cannot use '{SelectedItem}' on '{target}'.");
            }

            IsDoorOpen = true;
            SelectedItem = "";
        }
    }
}
