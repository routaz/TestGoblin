using System.Collections.Generic;
using System.Threading.Tasks;

namespace Routaz.TestGoblin.Runtime.Backend
{
    /// <summary>
    /// Test-only seam onto the game's backend (player progress, inventory,
    /// Remote Config / A-B variants). Lets a test pin a deterministic starting
    /// state and assert on server-side state that the UI alone cannot reveal.
    ///
    /// Implementations talk to a real test/staging backend; the in-memory fake
    /// in the test assembly proves the framework wiring without one.
    /// </summary>
    public interface IBackendTestApi
    {
        /// <summary>
        /// Force a specific Remote Config / A-B variant for the run, so
        /// experiment bucketing can't make a test flaky. Key = config flag,
        /// variant = the value to pin.
        /// </summary>
        Task PinRemoteConfigVariant(string key, string variant);

        /// <summary>
        /// Reset the player to a known starting state before a test — the
        /// mechanism behind test independence (e.g. a fresh player for FTUE).
        /// </summary>
        Task SeedPlayerState(PlayerProfile profile);

        /// <summary>
        /// Read the player's current inventory (item-key -> count) for state
        /// assertions — e.g. verifying a claimed reward actually landed.
        /// </summary>
        Task<IReadOnlyDictionary<string, int>> GetInventory();

        /// <summary>
        /// Snapshot of the config/variants active for this run. The triage
        /// artifact when a test fails against unknown live config (Q2).
        /// </summary>
        Task<IReadOnlyDictionary<string, string>> GetActiveConfigSnapshot();

        /// <summary>
        /// Clear all test-controlled state (player, inventory, pinned variants) back
        /// to empty, so no state leaks between tests. Called by BaseTest per test.
        /// </summary>
        Task Reset();
    }
}
