using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Routaz.TestGoblin.Runtime.Driver
{
    /// <summary>
    /// Shared behaviour for every in-process driver that clicks things by dispatching
    /// pointer events.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Finding objects, waiting, reading text, and the tap/drag event sequences are identical
    /// whether the game is built from uGUI widgets, 2D sprites or 3D meshes. The only thing
    /// that genuinely differs is <em>hit-testing</em>: deciding where an object is on screen
    /// and what a click there would actually reach.
    /// </para>
    /// <para>
    /// This class exists because that duplication had a measurable cost. A stale-transform
    /// bug in the raycast had to be fixed in two drivers independently, and would have needed
    /// a third had the uGUI driver used physics. Subclasses now supply four small pieces and
    /// inherit everything else.
    /// </para>
    /// <para>
    /// Play mode only: the waits need frames, and <see cref="SettleAsync"/> yields one.
    /// </para>
    /// </remarks>
    public abstract class PointerGameDriverBase : ILayeredGameDriver
    {
        private readonly float _defaultTimeout;
        private readonly int _defaultPollInterval;

        /// <param name="defaultTimeout">Seconds before a wait gives up.</param>
        /// <param name="defaultPollInterval">Milliseconds between polls.</param>
        protected PointerGameDriverBase(float defaultTimeout, int defaultPollInterval)
        {
            _defaultTimeout = defaultTimeout;
            _defaultPollInterval = defaultPollInterval;
        }

        /// <summary>Where on screen to aim at this object.</summary>
        /// <param name="target">The object being acted on.</param>
        /// <param name="action">Verb for the error message, e.g. "tap".</param>
        protected abstract Vector2 ScreenPositionOf(GameObject target, string action);

        /// <summary>
        /// What a click at this screen position would actually reach, or null if nothing.
        /// </summary>
        protected abstract GameObject TopMostAt(Vector2 screenPosition);

        /// <summary>
        /// Driver-specific reason nothing was hit, completing the sentence
        /// "It may be off-screen, or …".
        /// </summary>
        protected abstract string NothingHitHint { get; }

        /// <summary>Whether a player could act on this object right now.</summary>
        protected abstract bool CanPlayerActOn(GameObject target);

        /// <summary>
        /// Driver-specific reason an object is not actionable, completing the sentence
        /// "Cannot tap 'X' — …".
        /// </summary>
        /// <remarks>
        /// Kept per-driver rather than unified, because "not interactable" means something
        /// different in each: a disabled Selectable, or a missing collider. A generic message
        /// would send someone looking in the wrong place.
        /// </remarks>
        protected abstract string NotActionableReason { get; }

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
        /// Finds an active scene object by name, or returns null if it is absent or inactive.
        /// </summary>
        /// <param name="name">The GameObject's name.</param>
        /// <param name="id">Ignored — present for out-of-process drivers that address objects by id.</param>
        /// <remarks>
        /// Inactive objects are deliberately invisible: a player cannot see or touch them, so
        /// neither should a test. That is also what makes <see cref="WaitForObjectGone"/>
        /// meaningful for a screen that is merely deactivated rather than destroyed.
        /// </remarks>
        public Task<GoblinObject> FindObject(string name, int id = 0)
        {
            GameObject found = GameObject.Find(name);

            if (found == null)
            {
                return Task.FromResult<GoblinObject>(null);
            }

            return Task.FromResult(new GoblinObject
            {
                Name = found.name,
                Enabled = found.activeInHierarchy && CanPlayerActOn(found)
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
            }, _defaultTimeout, _defaultPollInterval, $"object '{name}' to appear");

            return found;
        }

        /// <inheritdoc />
        public Task WaitForObjectGone(string name, int id = 0)
        {
            return WaitForCondition(async () => await FindObject(name, id) == null,
                _defaultTimeout, _defaultPollInterval, $"object '{name}' to disappear");
        }

        /// <inheritdoc />
        public Task WaitForCondition(Func<Task<bool>> condition, float timeout, int pollIntervalMs, string description)
        {
            return Waits.WaitForCondition(condition, timeout, pollIntervalMs, description);
        }

        /// <summary>
        /// Clicks an object the way a player would: only if it is actionable and actually reachable.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// The object is gone, cannot be acted on, or is not what a click at its position would hit.
        /// </exception>
        public async Task Tap(GoblinObject goblinObject)
        {
            GameObject target = RequireObject(goblinObject.Name, "tap");
            RequireActionable(target, "tap");

            var pointer = new PointerEventData(EventSystem.current)
            {
                position = ScreenPositionOf(target, "tap"),
                button = PointerEventData.InputButton.Left
            };

            RequireOnScreen(pointer.position, target, "tap");
            RequireReachable(pointer.position, target, "tap");

            // The sequence a real click produces. Buttons respond to the click handler, but
            // emitting down/up too keeps anything watching press state behaving normally.
            ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(target, pointer, ExecuteEvents.pointerClickHandler);

            await SettleAsync();
        }

        /// <summary>
        /// Drags one object onto another using the standard pointer drag sequence.
        /// </summary>
        /// <exception cref="InvalidOperationException">Either object is gone or unreachable.</exception>
        public async Task Drag(GoblinObject sourceObject, GoblinObject destinationObject)
        {
            GameObject source = RequireObject(sourceObject.Name, "drag from");
            GameObject destination = RequireObject(destinationObject.Name, "drag to");

            RequireActionable(source, "drag from");

            Vector2 start = ScreenPositionOf(source, "drag from");
            Vector2 end = ScreenPositionOf(destination, "drag to");

            var pointer = new PointerEventData(EventSystem.current)
            {
                position = start,
                button = PointerEventData.InputButton.Left,
                pointerDrag = source
            };

            RequireOnScreen(start, source, "drag from");
            RequireOnScreen(end, destination, "drag to");
            RequireReachable(start, source, "drag from");

            ExecuteEvents.ExecuteHierarchy(source, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(source, pointer, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.ExecuteHierarchy(source, pointer, ExecuteEvents.beginDragHandler);

            pointer.delta = end - start;
            pointer.position = end;
            ExecuteEvents.ExecuteHierarchy(source, pointer, ExecuteEvents.dragHandler);

            ExecuteEvents.ExecuteHierarchy(source, pointer, ExecuteEvents.endDragHandler);
            ExecuteEvents.ExecuteHierarchy(destination, pointer, ExecuteEvents.dropHandler);
            ExecuteEvents.ExecuteHierarchy(source, pointer, ExecuteEvents.pointerUpHandler);

            await SettleAsync();
        }

        /// <summary>
        /// Reads text from the object or its children.
        /// </summary>
        /// <remarks>
        /// Children are searched because a tester naming a button means the label they can
        /// see, which is virtually always a child of it.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The object is gone or carries no text.</exception>
        public Task<string> GetText(GoblinObject goblinObject)
        {
            GameObject target = RequireObject(goblinObject.Name, "read text of");

            TMP_Text textMeshPro = target.GetComponentInChildren<TMP_Text>(includeInactive: true);

            if (textMeshPro != null)
            {
                return Task.FromResult(textMeshPro.text);
            }

            Text legacyText = target.GetComponentInChildren<Text>(includeInactive: true);

            if (legacyText != null)
            {
                return Task.FromResult(legacyText.text);
            }

            throw new InvalidOperationException(
                $"'{goblinObject.Name}' has no TMP_Text or Text component on itself or its children.");
        }

        /// <inheritdoc />
        public string WhatIsAt(Vector2 screenPosition)
        {
            GameObject hit = TopMostAt(screenPosition);

            return hit == null ? null : hit.name;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Returns null when the object exists but has no meaningful screen position — the
        /// 3D driver rejects anything behind the camera, for instance. A caller asking where
        /// something is wants "nowhere useful", not an exception.
        /// </remarks>
        public Vector2? TryScreenPositionOf(string name)
        {
            GameObject found = GameObject.Find(name);

            if (found == null)
            {
                return null;
            }

            try
            {
                return ScreenPositionOf(found, "locate");
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        /// <summary>Resolves a name to an active object, or explains that it is not there.</summary>
        protected static GameObject RequireObject(string name, string action)
        {
            GameObject found = GameObject.Find(name);

            if (found == null)
            {
                throw new InvalidOperationException(
                    $"Cannot {action} '{name}' — no active object with that name is in the scene.");
            }

            return found;
        }

        private void RequireActionable(GameObject target, string action)
        {
            if (!CanPlayerActOn(target))
            {
                throw new InvalidOperationException(
                    $"Cannot {action} '{target.name}' — {NotActionableReason}.");
            }
        }

        /// <summary>
        /// Verifies the object is somewhere a player could actually point at.
        /// </summary>
        /// <remarks>
        /// A raycast does not care about the viewport: <c>ScreenPointToRay</c> extrapolates
        /// beyond the screen edges, so an object placed outside the view is still hit and
        /// still "clicked". Without this check a test can pass against something no player
        /// can see — which is precisely the false green the reachability check exists to
        /// prevent, one step earlier.
        /// </remarks>
        private static void RequireOnScreen(Vector2 position, GameObject target, string action)
        {
            if (position.x >= 0f && position.y >= 0f
                && position.x <= Screen.width && position.y <= Screen.height)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Cannot {action} '{target.name}' — it is off-screen at {position}, " +
                $"and the view is {Screen.width}x{Screen.height}. A player could not reach it.");
        }

        /// <summary>
        /// Verifies the object is what a click at the given position would actually reach.
        /// </summary>
        /// <remarks>
        /// This is what separates a simulated click from calling a handler directly. Without
        /// it, an object hidden behind something else would still "work", and the test would
        /// pass on a build a player could not operate.
        /// </remarks>
        private void RequireReachable(Vector2 screenPosition, GameObject target, string action)
        {
            GameObject topMost = TopMostAt(screenPosition);

            if (topMost == null)
            {
                throw new InvalidOperationException(
                    $"Cannot {action} '{target.name}' — nothing is hit at {screenPosition}. " +
                    $"It may be off-screen, or {NothingHitHint}.");
            }

            // IsChildOf is true for the object itself, which covers the common case of naming
            // a parent whose child carries the actual graphic or collider.
            if (topMost.transform.IsChildOf(target.transform) || target.transform.IsChildOf(topMost.transform))
            {
                return;
            }

            throw new InvalidOperationException(
                $"Cannot {action} '{target.name}' — '{topMost.name}' is in front of it at {screenPosition}.");
        }

        /// <summary>
        /// Gives the game a frame to react to simulated input before anything else happens.
        /// </summary>
        /// <remarks>
        /// Every other operation completes synchronously, so without this an entire scenario
        /// can run inside one frame and the game never gets to respond. uGUI in particular
        /// rebuilds canvas geometry at end of frame, so a button a tap has just revealed is
        /// not yet registered with the raycaster and reads as "nothing is hit-testable".
        /// </remarks>
        protected static async Task SettleAsync()
        {
            await Awaitable.NextFrameAsync();
        }
    }
}
