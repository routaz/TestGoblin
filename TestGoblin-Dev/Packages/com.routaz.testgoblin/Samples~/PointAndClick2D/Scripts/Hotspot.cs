using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DemoGame.Adventure
{
    /// <summary>
    /// A clickable sprite.
    /// </summary>
    /// <remarks>
    /// Handles <see cref="IPointerClickHandler"/> rather than Unity's <c>OnMouseDown</c>
    /// message. That is what makes the object drivable: <c>PhysicsGameDriver</c> dispatches
    /// pointer events, and a real player's click arrives the same way via the camera's
    /// <c>Physics2DRaycaster</c> — so both take an identical path through the game.
    /// </remarks>
    [RequireComponent(typeof(Collider2D))]
    public sealed class Hotspot : MonoBehaviour, IPointerClickHandler
    {
        private Action _onClick;

        public void Initialise(Action onClick)
        {
            _onClick = onClick;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onClick?.Invoke();
        }
    }
}
