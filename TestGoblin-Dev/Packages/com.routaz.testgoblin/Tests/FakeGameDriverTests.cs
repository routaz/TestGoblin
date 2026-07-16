using System;
using System.Threading.Tasks;
using NUnit.Framework;
using Routaz.TestGoblin.Runtime.Driver;

namespace Routaz.TestGoblin.Tests
{
    public class FakeGameDriverTests
    {
        // NOTE: NUnit's Assert.ThrowsAsync / DoesNotThrowAsync deadlock Unity's
        // Test Runner when the task genuinely suspends (they block the main
        // thread internally). Use plain async tests + this helper instead.
        private static async Task AssertThrowsAsync<TException>(Func<Task> action)
            where TException : Exception
        {
            try
            {
                await action();
            }
            catch (TException)
            {
                return; // expected
            }

            Assert.Fail($"Expected {typeof(TException).Name} was not thrown.");
        }

        [Test]
        public async Task AsyncSanity_TaskDelay_Completes()
        {
            await Task.Delay(100);
            Assert.Pass();
        }

        [Test]
        public async Task WaitForObject_ObjectAppearsBeforeTimeout_ReturnsObject()
        {
            var driver = new FakeGameDriver(defaultTimeout: 2f, defaultPollInterval: 50);
            driver.ScriptObject("ClaimButton", appearsAfterMs: 200);

            GoblinObject result = await driver.WaitForObject("ClaimButton");

            Assert.NotNull(result);
            Assert.AreEqual("ClaimButton", result.Name);
        }

        [Test]
        public async Task WaitForObject_ObjectNeverAppears_ThrowsTimeoutException()
        {
            var driver = new FakeGameDriver(defaultTimeout: 0.3f, defaultPollInterval: 50);

            await AssertThrowsAsync<TimeoutException>(() => driver.WaitForObject("Nonexistent"));
        }

        [Test]
        public async Task WaitForObjectGone_ObjectRemovedBeforeTimeout_CompletesSuccessfully()
        {
            var driver = new FakeGameDriver(defaultTimeout: 2f, defaultPollInterval: 50);
            driver.ScriptObject("Overlay", appearsAfterMs: 0);

            _ = Task.Run(async () =>
            {
                await Task.Delay(150);
                driver.RemoveScriptedObject("Overlay");
            });

            // Completes without throwing = pass; a TimeoutException fails the test.
            await driver.WaitForObjectGone("Overlay");
        }

        [Test]
        public async Task WaitForObjectGone_ObjectNeverRemoved_ThrowsTimeoutException()
        {
            var driver = new FakeGameDriver(defaultTimeout: 0.3f, defaultPollInterval: 50);
            driver.ScriptObject("Overlay", appearsAfterMs: 0);

            await AssertThrowsAsync<TimeoutException>(() => driver.WaitForObjectGone("Overlay"));
        }

        [Test]
        public async Task Tap_ObjectAvailable_RecordsTap()
        {
            var driver = new FakeGameDriver(defaultTimeout: 2f, defaultPollInterval: 50);
            driver.ScriptObject("ClaimButton", appearsAfterMs: 0);
            GoblinObject claimButton = await driver.FindObject("ClaimButton");

            await driver.Tap(claimButton);

            CollectionAssert.Contains(driver.TappedObjectNames, "ClaimButton");
        }

        [Test]
        public async Task Tap_ObjectNotAvailable_Throws()
        {
            var driver = new FakeGameDriver(defaultTimeout: 2f, defaultPollInterval: 50);
            var nonexistent = new GoblinObject { Name = "Nonexistent" };

            await AssertThrowsAsync<InvalidOperationException>(() => driver.Tap(nonexistent));
        }

        [Test]
        public async Task Drag_BothObjectsAvailable_RecordsDrag()
        {
            var driver = new FakeGameDriver(defaultTimeout: 2f, defaultPollInterval: 50);
            driver.ScriptObject("GemA", appearsAfterMs: 0);
            driver.ScriptObject("GemB", appearsAfterMs: 0);
            GoblinObject gemA = await driver.FindObject("GemA");
            GoblinObject gemB = await driver.FindObject("GemB");

            await driver.Drag(gemA, gemB);

            CollectionAssert.Contains(driver.DraggedPairs, ("GemA", "GemB"));
        }

        [Test]
        public async Task Drag_DestinationNotAvailable_Throws()
        {
            var driver = new FakeGameDriver(defaultTimeout: 2f, defaultPollInterval: 50);
            driver.ScriptObject("GemA", appearsAfterMs: 0);
            GoblinObject gemA = await driver.FindObject("GemA");
            var nonexistent = new GoblinObject { Name = "Nonexistent" };

            await AssertThrowsAsync<InvalidOperationException>(() => driver.Drag(gemA, nonexistent));
        }

        [Test]
        public async Task FindObject_ObjectNotYetAvailable_ReturnsNull()
        {
            var driver = new FakeGameDriver(defaultTimeout: 2f, defaultPollInterval: 50);
            driver.ScriptObject("ClaimButton", appearsAfterMs: 5000);

            GoblinObject result = await driver.FindObject("ClaimButton");

            Assert.IsNull(result);
        }
    }
}
