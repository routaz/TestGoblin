using System;
using System.Linq;
using UnityEngine;

namespace Routaz.TestGoblin.Runtime.Driver
{
    /// <summary>
    /// Drives a 2D scene whose interactive objects are sprites with colliders.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For point-and-click and other sprite-driven games, where uGUI raycasting does not
    /// apply. Clicks arrive as pointer events, so the game must handle
    /// <c>IPointerClickHandler</c> — as it would with a <c>Physics2DRaycaster</c> on the
    /// camera. Games relying on Unity's <c>OnMouseDown</c> messages are not supported:
    /// delivering those would mean calling into game code by name rather than simulating a
    /// click, and would succeed on a sprite no player could reach.
    /// </para>
    /// </remarks>
    public sealed class PhysicsGameDriver : PointerGameDriverBase
    {
        private Camera _camera;

        /// <param name="defaultTimeout">Seconds before a wait gives up.</param>
        /// <param name="defaultPollInterval">Milliseconds between polls.</param>
        /// <param name="camera">
        /// Camera used to convert world positions to screen space. Defaults to
        /// <see cref="Camera.main"/>, resolved lazily so the driver can outlive a scene load.
        /// </param>
        public PhysicsGameDriver(float defaultTimeout, int defaultPollInterval, Camera camera = null)
            : base(defaultTimeout, defaultPollInterval)
        {
            _camera = camera;
        }

        /// <inheritdoc />
        protected override string NothingHitHint => "it may have no Collider2D";

        /// <inheritdoc />
        protected override string NotActionableReason => "it has no enabled Collider2D";

        /// <inheritdoc />
        protected override bool CanPlayerActOn(GameObject target)
        {
            var collider = target.GetComponentInChildren<Collider2D>();

            return collider != null && collider.enabled;
        }

        /// <inheritdoc />
        protected override Vector2 ScreenPositionOf(GameObject target, string action)
        {
            return RequireCamera().WorldToScreenPoint(target.transform.position);
        }

        /// <summary>
        /// Picks the sprite a player would perceive as topmost.
        /// </summary>
        /// <remarks>
        /// Sorting comes before distance, because 2D scenes routinely stack sprites at
        /// identical depth and order them purely by sorting layer. Choosing by distance alone
        /// would pick the wrong sprite and make the reachability check worse than useless.
        /// </remarks>
        protected override GameObject TopMostAt(Vector2 screenPosition)
        {
            Camera camera = RequireCamera();

            // Transform changes since the last simulation step are invisible to raycasts
            // unless synced — autoSyncTransforms is off by default. Without this the driver
            // hit-tests against where things used to be.
            Physics2D.SyncTransforms();

            RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(camera.ScreenPointToRay(screenPosition));

            if (hits.Length == 0)
            {
                return null;
            }

            return hits
                .OrderByDescending(SortingLayerValue)
                .ThenByDescending(SortingOrder)
                .ThenBy(hit => hit.distance)
                .First()
                .collider.gameObject;
        }

        private static int SortingLayerValue(RaycastHit2D hit)
        {
            Renderer renderer = hit.collider.GetComponentInChildren<Renderer>();

            return renderer == null ? int.MinValue : SortingLayer.GetLayerValueFromID(renderer.sortingLayerID);
        }

        private static int SortingOrder(RaycastHit2D hit)
        {
            Renderer renderer = hit.collider.GetComponentInChildren<Renderer>();

            return renderer == null ? int.MinValue : renderer.sortingOrder;
        }

        private Camera RequireCamera()
        {
            if (_camera != null)
            {
                return _camera;
            }

            _camera = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>();

            if (_camera == null)
            {
                throw new InvalidOperationException(
                    "No camera in the scene. A physics driver needs one to convert world " +
                    "positions to screen space — tag your camera MainCamera, or pass it in.");
            }

            return _camera;
        }
    }
}
