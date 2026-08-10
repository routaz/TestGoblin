using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace Routaz.TestGoblin.Runtime.Driver
{
    /// <summary>
    /// Drives a game built from several UI technologies at once — a uGUI or UI Toolkit HUD
    /// over a 2D or 3D world.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the normal shape of a real game: an inventory, a pause menu and a dialogue box
    /// drawn over a world you also click. No single driver can see all of it, and delegating
    /// by ownership alone would not be enough — a world driver's raycast cannot see a UI panel
    /// on top of it, so it would cheerfully click a door that is currently behind an open
    /// inventory. That false green is the whole reason this class exists.
    /// </para>
    /// <para>
    /// Layers are ordered <b>topmost first</b>, matching draw order: UI Toolkit, then uGUI,
    /// then the world. Before acting on an object, every layer above the one that owns it is
    /// asked whether it covers that point.
    /// </para>
    /// </remarks>
    public sealed class CompositeGameDriver : IGameDriver
    {
        private readonly IReadOnlyList<ILayeredGameDriver> _layers;
        private readonly float _defaultTimeout;
        private readonly int _defaultPollInterval;

        /// <param name="defaultTimeout">Seconds before a wait gives up.</param>
        /// <param name="defaultPollInterval">Milliseconds between polls.</param>
        /// <param name="layers">Drivers ordered topmost first.</param>
        public CompositeGameDriver(
            float defaultTimeout, int defaultPollInterval, params ILayeredGameDriver[] layers)
        {
            if (layers == null || layers.Length == 0)
            {
                throw new ArgumentException("A composite driver needs at least one layer.", nameof(layers));
            }

            _defaultTimeout = defaultTimeout;
            _defaultPollInterval = defaultPollInterval;
            _layers = layers;
        }

        /// <inheritdoc />
        public async Task Connect()
        {
            foreach (ILayeredGameDriver layer in _layers)
            {
                await layer.Connect();
            }
        }

        /// <inheritdoc />
        public async Task Disconnect()
        {
            foreach (ILayeredGameDriver layer in _layers)
            {
                await layer.Disconnect();
            }
        }

        /// <summary>
        /// Finds an object in the topmost layer that has one by this name.
        /// </summary>
        /// <remarks>
        /// Searched in draw order, so a name used in both the HUD and the world resolves to
        /// the one the player would actually be looking at.
        /// </remarks>
        public async Task<GoblinObject> FindObject(string name, int id = 0)
        {
            foreach (ILayeredGameDriver layer in _layers)
            {
                GoblinObject found = await layer.FindObject(name, id);

                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<GoblinObject> WaitForObject(string name, int id = 0)
        {
            GoblinObject found = null;

            await WaitForCondition(async () =>
            {
                found = await FindObject(name, id);
                return found != null;
            }, _defaultTimeout, _defaultPollInterval, $"object '{name}' to appear in any layer");

            return found;
        }

        /// <inheritdoc />
        public Task WaitForObjectGone(string name, int id = 0)
        {
            return WaitForCondition(async () => await FindObject(name, id) == null,
                _defaultTimeout, _defaultPollInterval, $"object '{name}' to disappear from every layer");
        }

        /// <inheritdoc />
        public Task WaitForCondition(Func<Task<bool>> condition, float timeout, int pollIntervalMs, string description)
        {
            return Waits.WaitForCondition(condition, timeout, pollIntervalMs, description);
        }

        /// <summary>
        /// Taps an object, after checking that nothing drawn above it is in the way.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// No layer has the object, or a higher layer covers it.
        /// </exception>
        public async Task Tap(GoblinObject goblinObject)
        {
            int owner = await RequireOwningLayer(goblinObject.Name, "tap");

            RequireNotCoveredByHigherLayer(goblinObject.Name, owner, "tap");

            await _layers[owner].Tap(goblinObject);
        }

        /// <summary>
        /// Drags one object onto another, provided both live in the same layer.
        /// </summary>
        /// <remarks>
        /// Cross-layer dragging — an inventory item from a UI panel onto a world object — is
        /// deliberately refused rather than faked. The pointer sequence would have to begin in
        /// one input system and end in another, and no amount of dispatching makes that a
        /// thing the game itself would ever see. Better to fail clearly than to pass on
        /// something a player cannot do.
        /// </remarks>
        public async Task Drag(GoblinObject sourceObject, GoblinObject destinationObject)
        {
            int sourceLayer = await RequireOwningLayer(sourceObject.Name, "drag from");
            int destinationLayer = await RequireOwningLayer(destinationObject.Name, "drag to");

            if (sourceLayer != destinationLayer)
            {
                throw new InvalidOperationException(
                    $"Cannot drag '{sourceObject.Name}' onto '{destinationObject.Name}' — they are in " +
                    "different layers, and a drag cannot start in one input system and finish in another.");
            }

            RequireNotCoveredByHigherLayer(sourceObject.Name, sourceLayer, "drag from");

            await _layers[sourceLayer].Drag(sourceObject, destinationObject);
        }

        /// <inheritdoc />
        public async Task<string> GetText(GoblinObject goblinObject)
        {
            int owner = await RequireOwningLayer(goblinObject.Name, "read text of");

            return await _layers[owner].GetText(goblinObject);
        }

        private async Task<int> RequireOwningLayer(string name, string action)
        {
            for (int index = 0; index < _layers.Count; index++)
            {
                if (await _layers[index].FindObject(name) != null)
                {
                    return index;
                }
            }

            throw new InvalidOperationException(
                $"Cannot {action} '{name}' — no layer has an object with that name.");
        }

        /// <summary>
        /// Fails if anything drawn above the owning layer occupies the object's position.
        /// </summary>
        /// <remarks>
        /// Skipped when the owner is already the topmost layer, and when its layer cannot
        /// report a screen position — in both cases there is nothing meaningful to compare.
        /// </remarks>
        private void RequireNotCoveredByHigherLayer(string name, int owner, string action)
        {
            if (owner == 0)
            {
                return;
            }

            Vector2? position = _layers[owner].TryScreenPositionOf(name);

            if (position == null)
            {
                return;
            }

            for (int above = 0; above < owner; above++)
            {
                string blocker = _layers[above].WhatIsAt(position.Value);

                if (blocker != null)
                {
                    throw new InvalidOperationException(
                        $"Cannot {action} '{name}' — '{blocker}' is drawn over it at {position.Value}. " +
                        "A player would hit the interface, not the object underneath.");
                }
            }
        }
    }
}
