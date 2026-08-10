using System.Collections.Generic;
using System.Threading.Tasks;

namespace Routaz.TestGoblin.Runtime.Backend
{
    /// <summary>
    /// In-memory <see cref="IBackendTestApi"/> for proof-level-1 tests: seeds
    /// and reads state from dictionaries, no real backend. Mirrors the
    /// FakeGameDriver pattern.
    /// </summary>
    public sealed class FakeBackendTestApi : IBackendTestApi
    {
        private PlayerProfile _profile = new PlayerProfile();
        private Dictionary<string, int> _inventory = new Dictionary<string, int>();
        private readonly Dictionary<string, string> _pinnedVariants = new Dictionary<string, string>();

        public PlayerProfile CurrentProfile => _profile;

        public Task PinRemoteConfigVariant(string key, string variant)
        {
            _pinnedVariants[key] = variant;
            return Task.CompletedTask;
        }

        public Task SeedPlayerState(PlayerProfile profile)
        {
            _profile = profile;
            _inventory = new Dictionary<string, int>(profile.Inventory);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyDictionary<string, int>> GetInventory()
        {
            return Task.FromResult<IReadOnlyDictionary<string, int>>(_inventory);
        }

        public Task<IReadOnlyDictionary<string, string>> GetActiveConfigSnapshot()
        {
            return Task.FromResult<IReadOnlyDictionary<string, string>>(_pinnedVariants);
        }

        /// <summary>
        /// Test helper (not on the interface): simulate the backend granting an
        /// item, so a test can assert an inventory change after a UI action.
        /// </summary>
        public void GrantItem(string itemKey, int count)
        {
            _inventory.TryGetValue(itemKey, out int current);
            _inventory[itemKey] = current + count;
        }

        public Task Reset()
        {
            _inventory.Clear();
            _pinnedVariants.Clear();
            _profile = new PlayerProfile();
            return Task.CompletedTask;
        }
    }
}
