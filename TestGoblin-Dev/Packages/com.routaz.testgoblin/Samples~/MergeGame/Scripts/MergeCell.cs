using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DemoGame.Merge
{
    /// <summary>
    /// One board cell: shows its tier, and accepts an item dragged onto it.
    /// </summary>
    /// <remarks>
    /// The merge happens in <see cref="OnDrop"/> rather than on the dragged object, because
    /// a drop is what a user actually performs — and it is what <c>InProcessGameDriver.Drag</c>
    /// dispatches to the destination. Doing it any other way would mean the driver's simulated
    /// input and a real player's input took different code paths.
    /// </remarks>
    [RequireComponent(typeof(Image))]
    public sealed class MergeCell : MonoBehaviour,
        IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
    {
        private MergeGameView _view;
        private TMP_Text _label;
        private GameObject _ghost;

        /// <summary>This cell's index into the board.</summary>
        public int Index { get; private set; }

        public void Initialise(MergeGameView view, int index, TMP_Text label)
        {
            _view = view;
            Index = index;
            _label = label;
        }

        /// <summary>Shows the tier, or nothing when the cell is empty.</summary>
        public void Show(int tier)
        {
            _label.text = tier == MergeBoard.Empty ? string.Empty : tier.ToString();
        }

        public void OnDrop(PointerEventData eventData)
        {
            if (eventData.pointerDrag == null)
            {
                return;
            }

            MergeCell source = eventData.pointerDrag.GetComponent<MergeCell>();

            if (source != null && source != this)
            {
                _view.TryMerge(source.Index, Index);
            }
        }

        /// <summary>
        /// Lifts a copy of the item under the pointer, so a human can see what they are dragging.
        /// </summary>
        /// <remarks>
        /// The driver dispatches these handlers too, so simulated and real drags run exactly
        /// the same code — the ghost simply appears and vanishes within a frame when a test
        /// does it. Keeping one path matters: a visual affordance that only humans trigger
        /// is a piece of the game no test ever touches.
        /// </remarks>
        public void OnBeginDrag(PointerEventData eventData)
        {
            if (string.IsNullOrEmpty(_label.text))
            {
                return;   // nothing in this cell to pick up
            }

            Canvas canvas = GetComponentInParent<Canvas>();

            if (canvas == null)
            {
                return;
            }

            _ghost = new GameObject("DragGhost", typeof(RectTransform), typeof(Image));
            _ghost.transform.SetParent(canvas.transform, worldPositionStays: false);
            _ghost.transform.SetAsLastSibling();   // above the board

            var ghostRect = (RectTransform)_ghost.transform;
            ghostRect.sizeDelta = ((RectTransform)transform).sizeDelta;

            var ghostImage = _ghost.GetComponent<Image>();
            ghostImage.color = new Color(0.4f, 0.7f, 1f, 0.75f);

            // Critical: the ghost must not be hit-testable, or it would sit under the
            // pointer and swallow the drop meant for the cell underneath.
            ghostImage.raycastTarget = false;

            var ghostLabelObject = new GameObject("Label", typeof(RectTransform));
            ghostLabelObject.transform.SetParent(_ghost.transform, worldPositionStays: false);

            var ghostLabelRect = (RectTransform)ghostLabelObject.transform;
            ghostLabelRect.anchorMin = Vector2.zero;
            ghostLabelRect.anchorMax = Vector2.one;
            ghostLabelRect.offsetMin = Vector2.zero;
            ghostLabelRect.offsetMax = Vector2.zero;

            TMP_Text ghostLabel = ghostLabelObject.AddComponent<TextMeshProUGUI>();
            ghostLabel.text = _label.text;
            ghostLabel.color = Color.black;
            ghostLabel.alignment = TextAlignmentOptions.Center;
            ghostLabel.fontSize = _label.fontSize;
            ghostLabel.raycastTarget = false;

            _ghost.transform.position = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_ghost != null)
            {
                _ghost.transform.position = eventData.position;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_ghost != null)
            {
                Destroy(_ghost);
                _ghost = null;
            }
        }
    }
}
