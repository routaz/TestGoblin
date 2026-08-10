using UnityEngine;

namespace Routaz.TestGoblin.Runtime.Driver
{
    /// <summary>
    /// A driver that can answer questions about its own visual layer, so several can be
    /// stacked by <see cref="CompositeGameDriver"/>.
    /// </summary>
    /// <remarks>
    /// Exists because the interesting problem in a mixed game is <em>cross-layer occlusion</em>.
    /// A world driver's raycast cannot see a UI panel drawn over it, so on its own it will
    /// happily click a door that is currently behind an open inventory. Composing drivers by
    /// ownership alone would keep that hole; the composite needs to ask upper layers what
    /// they are covering.
    /// <para>
    /// All positions here are <b>screen</b> coordinates, origin bottom-left, matching
    /// <c>Camera.WorldToScreenPoint</c>. Drivers whose native space differs convert.
    /// </para>
    /// </remarks>
    public interface ILayeredGameDriver : IGameDriver
    {
        /// <summary>
        /// The name of whatever this layer would hand a click at the given screen position,
        /// or null if this layer has nothing there.
        /// </summary>
        string WhatIsAt(Vector2 screenPosition);

        /// <summary>
        /// Where this layer places the named object on screen, or null if it has no such
        /// object or cannot determine a position for it.
        /// </summary>
        Vector2? TryScreenPositionOf(string name);
    }
}
