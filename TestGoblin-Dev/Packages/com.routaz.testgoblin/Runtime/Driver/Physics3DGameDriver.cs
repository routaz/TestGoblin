using System;
using System.Linq;
using UnityEngine;

namespace Routaz.TestGoblin.Runtime.Driver
{
    /// <summary>
    /// Drives a 3D scene whose interactive objects are meshes with colliders.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Simpler than the 2D driver, because 3D has real depth: the nearest hit along the ray
    /// <em>is</em> what the player clicks, so there is no sorting-order tie-break to get right.
    /// </para>
    /// <para>
    /// Occlusion is what this tier buys you. A door behind a wall fails here, and "the player
    /// can see it but cannot reach it" is a bug the raycast catches for free.
    /// </para>
    /// </remarks>
    public sealed class Physics3DGameDriver : PointerGameDriverBase
    {
        private Camera _camera;

        /// <param name="defaultTimeout">Seconds before a wait gives up.</param>
        /// <param name="defaultPollInterval">Milliseconds between polls.</param>
        /// <param name="camera">Camera used for picking. Defaults to <see cref="Camera.main"/>.</param>
        public Physics3DGameDriver(float defaultTimeout, int defaultPollInterval, Camera camera = null)
            : base(defaultTimeout, defaultPollInterval)
        {
            _camera = camera;
        }

        /// <inheritdoc />
        protected override string NothingHitHint => "it may have no Collider";

        /// <inheritdoc />
        protected override string NotActionableReason => "it has no enabled Collider";

        /// <inheritdoc />
        protected override bool CanPlayerActOn(GameObject target)
        {
            var collider = target.GetComponentInChildren<Collider>();

            return collider != null && collider.enabled;
        }

        /// <summary>
        /// Where on screen to aim at this object.
        /// </summary>
        /// <remarks>
        /// Uses the renderer's bounds centre rather than the transform pivot. A 3D model's
        /// pivot is commonly at its feet, which can sit inside the floor or off the visible
        /// silhouette — aiming there would miss an object the player can plainly see.
        /// </remarks>
        protected override Vector2 ScreenPositionOf(GameObject target, string action)
        {
            Renderer renderer = target.GetComponentInChildren<Renderer>();
            Vector3 worldPoint = renderer != null ? renderer.bounds.center : target.transform.position;

            Vector3 screenPoint = RequireCamera().WorldToScreenPoint(worldPoint);

            // Behind the camera comes back with negative depth and meaningless x/y, which
            // would otherwise surface as a confusing "nothing is hit" somewhere off-screen.
            if (screenPoint.z <= 0f)
            {
                throw new InvalidOperationException(
                    $"Cannot {action} '{target.name}' — it is behind the camera.");
            }

            return screenPoint;
        }

        /// <summary>
        /// The nearest thing along the ray, which in 3D is exactly what the player clicks.
        /// </summary>
        protected override GameObject TopMostAt(Vector2 screenPosition)
        {
            Camera camera = RequireCamera();

            // Unity leaves Physics.autoSyncTransforms off by default, so anything moved since
            // the last simulation step is still at its old place as far as raycasts are
            // concerned. A driver clicking right after the game moved something would
            // otherwise hit-test against stale geometry.
            Physics.SyncTransforms();

            RaycastHit[] hits = Physics.RaycastAll(camera.ScreenPointToRay(screenPosition));

            if (hits.Length == 0)
            {
                return null;
            }

            return hits.OrderBy(hit => hit.distance).First().collider.gameObject;
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
                    "No camera in the scene. A 3D driver needs one to pick objects — tag your " +
                    "camera MainCamera, or pass it in.");
            }

            return _camera;
        }
    }
}
