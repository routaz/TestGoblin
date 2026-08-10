using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Routaz.TestGoblin.Runtime.Driver
{
    /// <summary>
    /// Drives a live uGUI scene from inside Unity play mode.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The default driver. No licence, no TCP, no external application, so the whole suite
    /// runs free and headless in CI. The honest trade against the out-of-process driver is
    /// that it shares the game's process and cannot validate a real build on real hardware.
    /// </para>
    /// <para>
    /// uGUI-specific. A game built on 2D sprites or 3D meshes uses
    /// <see cref="PhysicsGameDriver"/> or <see cref="Physics3DGameDriver"/>; everything above
    /// the driver stays identical, which is the point of the seam.
    /// </para>
    /// </remarks>
    public sealed class InProcessGameDriver : PointerGameDriverBase
    {
        /// <param name="defaultTimeout">Seconds before a wait gives up.</param>
        /// <param name="defaultPollInterval">Milliseconds between polls.</param>
        public InProcessGameDriver(float defaultTimeout, int defaultPollInterval)
            : base(defaultTimeout, defaultPollInterval)
        {
        }

        /// <inheritdoc />
        protected override string NothingHitHint => "its Graphic may have Raycast Target disabled";

        /// <inheritdoc />
        protected override string NotActionableReason => "it is present but its Selectable is not interactable";

        /// <summary>
        /// Whether a player could act on this object: interactable, if it is a control at all.
        /// </summary>
        protected override bool CanPlayerActOn(GameObject target)
        {
            Selectable selectable = target.GetComponent<Selectable>();

            return selectable == null || selectable.interactable;
        }

        /// <summary>
        /// Converts the object's position to screen space, accounting for the canvas render mode.
        /// </summary>
        protected override Vector2 ScreenPositionOf(GameObject target, string action)
        {
            Canvas canvas = target.GetComponentInParent<Canvas>();

            // A ScreenSpaceOverlay canvas has no camera, and passing one would produce the
            // wrong coordinates rather than an error.
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            return RectTransformUtility.WorldToScreenPoint(camera, target.transform.position);
        }

        /// <summary>
        /// Asks the event system what a pointer here would hit, which is exactly what real
        /// input consults.
        /// </summary>
        protected override GameObject TopMostAt(Vector2 screenPosition)
        {
            if (EventSystem.current == null)
            {
                throw new System.InvalidOperationException(
                    "No EventSystem in the scene. uGUI cannot deliver pointer events without one — " +
                    "add GameObject > UI > Event System to the scene under test.");
            }

            var pointer = new PointerEventData(EventSystem.current) { position = screenPosition };
            var hits = new List<RaycastResult>();

            EventSystem.current.RaycastAll(pointer, hits);

            return hits.Count == 0 ? null : hits[0].gameObject;
        }
    }
}
