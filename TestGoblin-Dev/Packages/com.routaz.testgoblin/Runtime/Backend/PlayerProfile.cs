using System.Collections.Generic;

namespace Routaz.TestGoblin.Runtime.Backend
{
    /// <summary>
    /// Minimal server-side player state a test needs to seed a deterministic
    /// starting point. Deliberately tiny — a real backend profile has far more,
    /// but tests only pin what they assert on.
    /// </summary>
    public sealed class PlayerProfile
    {
        /// <summary>Whether the tutorial has already been completed for this player.</summary>
        public bool HasCompletedTutorial { get; init; }

        /// <summary>Whether the player is eligible to claim today's daily reward.</summary>
        public bool IsDailyRewardAvailable { get; init; }

        /// <summary>Starting inventory as item-key -> count.</summary>
        public IReadOnlyDictionary<string, int> Inventory { get; init; } = new Dictionary<string, int>();
    }
}
