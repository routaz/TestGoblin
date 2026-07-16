using Routaz.TestGoblin.Runtime.Config;
using System;
using System.Threading.Tasks;


namespace Routaz.TestGoblin.Runtime.Driver
{
    /// <summary>
    /// IGameDriver implementation backed by AltTester. Compile-stubbed:
    /// the AltTester SDK is not installed in this project, so each method
    /// documents the intended AltTester 2.x call and throws.
    /// </summary>
    public sealed class AltTesterGameDriver : IGameDriver
    {

        private readonly TestRunConfig _config;
        // TODO(verify): private AltDriver _altDriver; — AltTester 2.x client type

        public AltTesterGameDriver(TestRunConfig config)
        {
            _config = config;
        }

        public Task Connect(string deviceId, string altTesterHost, int altTesterPort)
        {
            // TODO(verify): _altDriver = new AltDriver(altTesterHost, altTesterPort);
            throw new NotImplementedException("Requires the AltTester SDK — see README 'What's actually verified'.");
        }

        public Task Disconnect()
        {
            // TODO(verify): _altDriver.Stop();
            throw new NotImplementedException("Requires the AltTester SDK — see README 'What's actually verified'.");
        }

        public Task Drag(GoblinObject sourceObject, GoblinObject destinationObject)
        {
            // TODO(verify): resolve both objects' screen coords, then _altDriver.Swipe(start, end, duration);
            throw new NotImplementedException("Requires the AltTester SDK — see README 'What's actually verified'.");
        }

        public Task<GoblinObject> FindObject(string name, int id = 0)
        {
            /*
             * var altObject = _altDriver.FindObject(By.NAME, name);
            */
            throw new NotImplementedException("Requires the AltTester SDK — see README 'What's actually verified'.");
        }

        public Task Tap(GoblinObject goblinObject)
        {
            /*
                var testObject = altDriver.FindObject(By.NAME, goblinObject.Name);
                testObject.Tap();
            */

            throw new NotImplementedException("Requires the AltTester SDK — see README 'What's actually verified'.");
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