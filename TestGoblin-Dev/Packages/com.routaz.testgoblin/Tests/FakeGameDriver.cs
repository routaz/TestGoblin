using Routaz.TestGoblin.Runtime.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Routaz.TestGoblin.Tests
{
    public class FakeGameDriver : IGameDriver
    {
        private readonly float defaultTimeout;
        private readonly int defaultPollInterval;
        private readonly Dictionary<string, ScriptedObject> _scriptedObjects = new Dictionary<string, ScriptedObject>();
        private readonly List<string> _tappedObjectNames = new List<string>();
        public IReadOnlyList<string> TappedObjectNames => _tappedObjectNames;
        private readonly List<(string From, string To)> _draggedPairs = new List<(string From, string To)>();
        public IReadOnlyList<(string From, string To)> DraggedPairs => _draggedPairs;

        public FakeGameDriver(float defaultTimeout,int defaultPollInterval)
        {
            this.defaultTimeout = defaultTimeout;
            this.defaultPollInterval = defaultPollInterval;
        }

        private sealed class ScriptedObject
        {
            public string Name { get; }
            public bool Enabled { get; }
            private readonly DateTime _availableAt;

            public ScriptedObject(string name, bool enabled, int appearsAfterMs)
            {
                Name = name;
                Enabled = enabled;
                _availableAt = DateTime.UtcNow.AddMilliseconds(appearsAfterMs);
            }

            public bool IsAvailable => DateTime.UtcNow >= _availableAt;
        }

        public Task Connect(string deviceId, string altTesterHost, int altTesterPort)
        {
            return Task.CompletedTask; //Simulate a successful connection
        }

        public Task Disconnect()
        {
            return Task.CompletedTask; //Simulate a successful disconnection
        }

        public Task Drag(GoblinObject sourceObject, GoblinObject destinationObject)
        {
            if (!_scriptedObjects.TryGetValue(sourceObject.Name, out ScriptedObject source) || !source.IsAvailable)
            {
                throw new InvalidOperationException($"Cannot drag from '{sourceObject.Name}' — it is not currently available.");
            }
            if (!_scriptedObjects.TryGetValue(destinationObject.Name, out ScriptedObject destination) || !destination.IsAvailable)
            {
                throw new InvalidOperationException($"Cannot drag to '{destinationObject.Name}' — it is not currently available.");
            }

            _draggedPairs.Add((sourceObject.Name, destinationObject.Name));
            return Task.CompletedTask;
        }

        public Task<GoblinObject> FindObject(string name, int id = 0)
        {
            if (_scriptedObjects.TryGetValue(name, out ScriptedObject scripted) && scripted.IsAvailable)
            {
                return Task.FromResult(new GoblinObject
                {
                    Name = scripted.Name,
                    Enabled = scripted.Enabled
                });
            }

            return Task.FromResult<GoblinObject>(null);
        }

        public Task Tap(GoblinObject goblinObject)
        {
            if (!_scriptedObjects.TryGetValue(goblinObject.Name, out ScriptedObject scripted) || !scripted.IsAvailable)
            {
                throw new InvalidOperationException($"Cannot tap '{goblinObject.Name}' — it is not currently available.");
            }

            _tappedObjectNames.Add(goblinObject.Name);
            return Task.CompletedTask;
        }

        public Task WaitForCondition(Func<Task<bool>> condition, float timeout, int pollIntervalMs, string description)
        {
            return Waits.WaitForCondition(condition, timeout, pollIntervalMs, description);
        }

        public async Task<GoblinObject> WaitForObject(string name, int id = 0)
        {
            GoblinObject found = null;
            
            await WaitForCondition(async () =>
            {
                found = await FindObject(name, id).ConfigureAwait(false);
                return found != null;
            }, defaultTimeout, defaultPollInterval, $"object '{name}' to appear").ConfigureAwait(false);
            
            return found;
        }

        public Task WaitForObjectGone(string name, int id = 0)
        {
            return WaitForCondition(async () => await FindObject(name, id).ConfigureAwait(false) == null,
                defaultTimeout, defaultPollInterval, $"object '{name}' to disappear");
        }

        public void ScriptObject(string name, int appearsAfterMs = 0, bool enabled = true)
        {
            _scriptedObjects[name] = new ScriptedObject(name, enabled, appearsAfterMs);
        }

        public void RemoveScriptedObject(string name)
        {
            _scriptedObjects.Remove(name);
        }
    }
}