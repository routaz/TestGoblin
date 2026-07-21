using Routaz.TestGoblin.Runtime.Config;
using System;
using System.Threading.Tasks;


#if ALTTESTER_PRESENT
using AltTester.AltTesterSDK.Driver;
#endif

namespace Routaz.TestGoblin.Runtime.Driver
{
    /// <summary>
    /// IGameDriver implementation backed by AltTester.
    ///
    /// The AltTester-specific code compiles only when the SDK is present
    /// (the ALTTESTER_PRESENT symbol, set via a Version Define on the package's
    /// Runtime assembly). Without the SDK the class still exists but its driver
    /// methods throw — so the framework, and the fake-driver tier, compile and
    /// run with no AltTester dependency at all. That keeps AltTester optional.
    ///
    /// AltDriver's API is synchronous (blocking TCP), so each call is wrapped in
    /// Task.Run to satisfy the async IGameDriver contract. This is safe because
    /// AltTester is out-of-process — nothing here touches Unity's main thread,
    /// which is exactly why the driver layer can use ConfigureAwait(false).
    /// </summary>
    public sealed class AltTesterGameDriver : IGameDriver
    {
        private readonly TestRunConfig _config;
#if ALTTESTER_PRESENT
        private AltDriver _altDriver;
#endif

        public AltTesterGameDriver(TestRunConfig config)
        {
            _config = config;
        }

#if ALTTESTER_PRESENT
        public Task Connect(string deviceId, string altTesterHost, int altTesterPort)
        {
            return Task.Run(() => { _altDriver = new AltDriver(altTesterHost, altTesterPort); });
        }

        public Task Disconnect()
        {
            return Task.Run(() => _altDriver?.Stop());
        }

        public Task<GoblinObject> FindObject(string name, int id = 0)
        {
            return Task.Run<GoblinObject>(() =>
            {
                try
                {
                    AltObject obj = _altDriver.FindObject(By.NAME, name);
                    return new GoblinObject { Name = obj.name, Id = obj.id, Enabled = obj.enabled };
                }
                catch (NotFoundException)
                {
                    // Honour the "null means not found" contract the waits rely on,
                    // rather than letting AltTester's exception bubble up.
                    return null;
                }
            });
        }

        public Task Tap(GoblinObject goblinObject)
        {
            return Task.Run(() =>
            {
                // Re-resolve by name at action time (GoblinObject is a value handle,
                // not a live reference) — also avoids stale-object flakiness.
                AltObject obj = _altDriver.FindObject(By.NAME, goblinObject.Name);
                obj.Tap();
            });
        }

        public Task Drag(GoblinObject sourceObject, GoblinObject destinationObject)
        {
            return Task.Run(() =>
            {
                AltObject source = _altDriver.FindObject(By.NAME, sourceObject.Name);
                AltObject destination = _altDriver.FindObject(By.NAME, destinationObject.Name);
                _altDriver.Swipe(source.GetScreenPosition(), destination.GetScreenPosition(), 1);
            });
        }

        public Task<string> GetText(GoblinObject goblinObject)
        {
            return Task.Run(() =>
            {
                AltObject obj = _altDriver.FindObject(By.NAME, goblinObject.Name);
                return obj.GetText();
            });
        }
#else
        public Task Connect(string deviceId, string altTesterHost, int altTesterPort) => throw NotInstalled();
        public Task Disconnect() => throw NotInstalled();
        public Task<GoblinObject> FindObject(string name, int id = 0) => throw NotInstalled();
        public Task Tap(GoblinObject goblinObject) => throw NotInstalled();
        public Task Drag(GoblinObject sourceObject, GoblinObject destinationObject) => throw NotInstalled();
        public Task<string> GetText(GoblinObject goblinObject) => throw NotInstalled();

        private static Exception NotInstalled() => new NotImplementedException(
            "AltTester SDK not present (ALTTESTER_PRESENT undefined). Install com.alttester.sdk to enable the real driver.");
#endif

        // The wait verbs compose FindObject + Waits and touch no AltTester types,
        // so they compile in both modes (they just throw via FindObject when the
        // SDK is absent).
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
            }, _config.DefaultTimeout, _config.DefaultPollInterval, $"object '{name}' to appear").ConfigureAwait(false);

            return found;
        }

        public Task WaitForObjectGone(string name, int id = 0)
        {
            return WaitForCondition(async () => await FindObject(name, id).ConfigureAwait(false) == null,
                _config.DefaultTimeout, _config.DefaultPollInterval, $"object '{name}' to disappear");
        }
    }
}
