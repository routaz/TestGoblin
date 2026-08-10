using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Routaz.TestGoblin.Runtime.Driver
{
    /// <summary>
    /// Drives a UI Toolkit interface, where controls are <see cref="VisualElement"/>s inside
    /// a <see cref="UIDocument"/> rather than GameObjects.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deliberately does not inherit <see cref="PointerGameDriverBase"/>. UI Toolkit shares
    /// none of that machinery: there are no GameObjects to find, no EventSystem, no
    /// raycaster, and no components to read text from. That it still satisfies
    /// <see cref="IGameDriver"/> is the strongest evidence the seam is real — every Screen,
    /// Flow and scenario above it is unchanged.
    /// </para>
    /// <para>
    /// Clicks are sent as a pointer down/up pair rather than as a <c>NavigationSubmitEvent</c>.
    /// Submit does activate a <c>Button</c>, but it is what a gamepad or keyboard produces —
    /// a custom element listening only for pointer events would silently ignore it, and the
    /// test would pass against a control no mouse user could operate.
    /// </para>
    /// <para>
    /// Play mode only, like every in-process driver.
    /// </para>
    /// </remarks>
    public sealed class UiToolkitGameDriver : ILayeredGameDriver
    {
        private readonly float _defaultTimeout;
        private readonly int _defaultPollInterval;

        /// <param name="defaultTimeout">Seconds before a wait gives up.</param>
        /// <param name="defaultPollInterval">Milliseconds between polls.</param>
        public UiToolkitGameDriver(float defaultTimeout, int defaultPollInterval)
        {
            _defaultTimeout = defaultTimeout;
            _defaultPollInterval = defaultPollInterval;
        }

        /// <summary>No-op: the game is already in this process.</summary>
        public Task Connect()
        {
            return Task.CompletedTask;
        }

        /// <summary>No-op: there is no connection to close.</summary>
        public Task Disconnect()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Finds a visible element by name across every <see cref="UIDocument"/> in the scene.
        /// </summary>
        /// <param name="name">The element's <c>name</c>, as set in UXML or code.</param>
        /// <param name="id">Ignored — present for out-of-process drivers that address objects by id.</param>
        /// <remarks>
        /// Hidden elements are treated as absent, mirroring how the other drivers ignore
        /// inactive GameObjects: a player cannot see or click them, so neither should a test.
        /// That is also what makes waiting for a panel to disappear meaningful.
        /// </remarks>
        public Task<GoblinObject> FindObject(string name, int id = 0)
        {
            VisualElement element = FindElement(name);

            if (element == null)
            {
                return Task.FromResult<GoblinObject>(null);
            }

            return Task.FromResult(new GoblinObject
            {
                Name = element.name,
                Enabled = element.enabledInHierarchy
            });
        }

        /// <inheritdoc />
        public async Task<GoblinObject> WaitForObject(string name, int id = 0)
        {
            GoblinObject found = null;

            await WaitForCondition(async () =>
            {
                found = await FindObject(name, id);
                return found != null;
            }, _defaultTimeout, _defaultPollInterval, $"element '{name}' to appear");

            return found;
        }

        /// <inheritdoc />
        public Task WaitForObjectGone(string name, int id = 0)
        {
            return WaitForCondition(async () => await FindObject(name, id) == null,
                _defaultTimeout, _defaultPollInterval, $"element '{name}' to disappear");
        }

        /// <inheritdoc />
        public Task WaitForCondition(Func<Task<bool>> condition, float timeout, int pollIntervalMs, string description)
        {
            return Waits.WaitForCondition(condition, timeout, pollIntervalMs, description);
        }

        /// <summary>
        /// Clicks an element, only if it is enabled and is what the panel reports at its own
        /// position.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The element is missing, disabled, or covered by another element.
        /// </exception>
        public async Task Tap(GoblinObject goblinObject)
        {
            VisualElement element = RequireElement(goblinObject.Name, "tap");

            if (!element.enabledInHierarchy)
            {
                throw new InvalidOperationException(
                    $"Cannot tap '{goblinObject.Name}' — it is present but disabled.");
            }

            Vector2 centre = element.worldBound.center;
            RequireReachable(element, centre, "tap");

            SendPointer(element, EventType.MouseDown, centre);
            SendPointer(element, EventType.MouseUp, centre);

            await SettleAsync();
        }

        /// <summary>
        /// Drags one element onto another as a pointer down, move and up.
        /// </summary>
        /// <remarks>
        /// UI Toolkit has no built-in drag protocol, so this is the raw pointer sequence an
        /// element's own drag handling would observe.
        /// </remarks>
        public async Task Drag(GoblinObject sourceObject, GoblinObject destinationObject)
        {
            VisualElement source = RequireElement(sourceObject.Name, "drag from");
            VisualElement destination = RequireElement(destinationObject.Name, "drag to");

            Vector2 start = source.worldBound.center;
            Vector2 end = destination.worldBound.center;

            RequireReachable(source, start, "drag from");

            SendPointer(source, EventType.MouseDown, start);
            SendPointer(source, EventType.MouseDrag, end);
            SendPointer(destination, EventType.MouseUp, end);

            await SettleAsync();
        }

        /// <summary>
        /// Reads an element's text.
        /// </summary>
        /// <exception cref="InvalidOperationException">The element carries no text.</exception>
        public Task<string> GetText(GoblinObject goblinObject)
        {
            VisualElement element = RequireElement(goblinObject.Name, "read text of");

            if (element is TextElement textElement)
            {
                return Task.FromResult(textElement.text);
            }

            // A container often holds a single label; reading it is what a tester means.
            TextElement child = element.Q<TextElement>();

            if (child != null)
            {
                return Task.FromResult(child.text);
            }

            throw new InvalidOperationException(
                $"'{goblinObject.Name}' is not a TextElement and contains none.");
        }

        /// <inheritdoc />
        /// <remarks>
        /// Converts through <see cref="RuntimePanelUtils.ScreenToPanel"/> rather than
        /// assuming panel space equals screen space. UI Toolkit's origin is top-left where
        /// the screen's is bottom-left, and a scaled panel adds a factor on top — so a raw
        /// comparison would look right at the centre and be wrong everywhere else.
        /// </remarks>
        public string WhatIsAt(Vector2 screenPosition)
        {
            foreach (UIDocument document in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                IPanel panel = document.rootVisualElement?.panel;

                if (panel == null)
                {
                    continue;
                }

                Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(panel, screenPosition);

                VisualElement picked = panel.Pick(panelPosition);

                if (picked != null)
                {
                    return picked.name;
                }
            }

            return null;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Always null. UI Toolkit is the topmost layer, so nothing is ever above it to be
        /// checked for occlusion — the composite only needs positions for the layers it has
        /// to look over the top of.
        /// </remarks>
        public Vector2? TryScreenPositionOf(string name)
        {
            return null;
        }

        /// <summary>
        /// Searches every UIDocument for a visible element with this name.
        /// </summary>
        private static VisualElement FindElement(string name)
        {
            UIDocument[] documents = UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None);

            foreach (UIDocument document in documents)
            {
                VisualElement root = document.rootVisualElement;

                if (root == null)
                {
                    continue;
                }

                VisualElement match = root.Q<VisualElement>(name);

                if (match != null && IsVisible(match))
                {
                    return match;
                }
            }

            return null;
        }

        private static VisualElement RequireElement(string name, string action)
        {
            VisualElement element = FindElement(name);

            if (element == null)
            {
                throw new InvalidOperationException(
                    $"Cannot {action} '{name}' — no visible element with that name is in any UIDocument.");
            }

            return element;
        }

        /// <summary>
        /// Whether the element and all its ancestors are actually shown.
        /// </summary>
        /// <remarks>
        /// UI Toolkit hides things two independent ways — <c>display: none</c> removes an
        /// element from layout, <c>visible: false</c> keeps its space but stops drawing it —
        /// and either means the player cannot see it. Both must be checked, up the whole
        /// chain, because a hidden parent hides its children without changing their own style.
        /// </remarks>
        private static bool IsVisible(VisualElement element)
        {
            for (VisualElement current = element; current != null; current = current.parent)
            {
                if (!current.visible || current.resolvedStyle.display == DisplayStyle.None)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Verifies the element is what the panel would hand a click at that position.
        /// </summary>
        /// <remarks>
        /// The UI Toolkit equivalent of the other drivers' raycast: <c>Pick</c> returns the
        /// topmost element, so a control underneath an overlay fails rather than being
        /// clicked through. The hit may legitimately be a child — a Button's internal label,
        /// for instance — so descendants count.
        /// </remarks>
        private static void RequireReachable(VisualElement element, Vector2 position, string action)
        {
            IPanel panel = element.panel;

            if (panel == null)
            {
                throw new InvalidOperationException(
                    $"Cannot {action} '{element.name}' — it is not attached to a panel.");
            }

            VisualElement picked = panel.Pick(position);

            if (picked == null)
            {
                throw new InvalidOperationException(
                    $"Cannot {action} '{element.name}' — nothing is pickable at {position}. " +
                    "It may be outside the panel, or have picking-mode Ignore.");
            }

            for (VisualElement current = picked; current != null; current = current.parent)
            {
                if (current == element)
                {
                    return;
                }
            }

            throw new InvalidOperationException(
                $"Cannot {action} '{element.name}' — '{picked.name}' is in front of it at {position}.");
        }

        /// <summary>
        /// Sends one synthetic pointer event.
        /// </summary>
        /// <remarks>
        /// Built from an IMGUI <see cref="Event"/> because that is the public route to a
        /// pooled pointer event with a real position — and position is what makes the
        /// difference between activating a control and merely poking it.
        /// </remarks>
        private static void SendPointer(VisualElement target, EventType type, Vector2 position)
        {
            var systemEvent = new Event
            {
                type = type,
                mousePosition = position,
                button = 0,
                clickCount = 1
            };

            switch (type)
            {
                case EventType.MouseDown:
                    using (var pointerDown = PointerDownEvent.GetPooled(systemEvent))
                    {
                        pointerDown.target = target;
                        target.SendEvent(pointerDown);
                    }

                    break;

                case EventType.MouseDrag:
                    using (var pointerMove = PointerMoveEvent.GetPooled(systemEvent))
                    {
                        pointerMove.target = target;
                        target.SendEvent(pointerMove);
                    }

                    break;

                case EventType.MouseUp:
                    using (var pointerUp = PointerUpEvent.GetPooled(systemEvent))
                    {
                        pointerUp.target = target;
                        target.SendEvent(pointerUp);
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported pointer event.");
            }
        }

        /// <summary>Gives the interface a frame to react before anything else happens.</summary>
        private static async Task SettleAsync()
        {
            await Awaitable.NextFrameAsync();
        }
    }
}
