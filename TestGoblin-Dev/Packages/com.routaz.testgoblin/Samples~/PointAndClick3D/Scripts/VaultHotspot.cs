using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DemoGame.Vault
{
    /// <summary>
    /// A clickable mesh.
    /// </summary>
    /// <remarks>
    /// Handles <see cref="IPointerClickHandler"/> rather than Unity's <c>OnMouseDown</c>
    /// message, so a simulated click from <c>Physics3DGameDriver</c> and a real player's click
    /// via the camera's <c>PhysicsRaycaster</c> take the same path through the game.
    /// </remarks>
    [RequireComponent(typeof(Collider))]
    public sealed class VaultHotspot : MonoBehaviour, IPointerClickHandler
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
