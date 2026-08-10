using System;
using System.Threading.Tasks;


namespace Routaz.TestGoblin.Runtime.Driver
{
    public static class Waits
    {
        /// <summary>
        /// Polls <paramref name="condition"/> until it returns true or the timeout expires.
        /// </summary>
        /// <remarks>
        /// Deliberately does NOT use ConfigureAwait(false). Unity's APIs are main-thread
        /// only, and in play mode the captured SynchronizationContext is what marshals
        /// each continuation back to the main thread. Discarding it would let the second
        /// and later polls resume on a threadpool thread, so any driver that touches the
        /// scene would work on the first poll and throw on every one after it — a bug
        /// that looks like flakiness rather than a defect.
        /// </remarks>
        public static async Task WaitForCondition(Func<Task<bool>> condition, float timeoutSeconds, int pollIntervalMs, string description)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (await condition())
                {
                    return;
                }
                await Task.Delay(pollIntervalMs);
                elapsed += pollIntervalMs / 1000f;
            }
            throw new TimeoutException($"Timed out after {timeoutSeconds}s waiting for {description}.");
        }

    }
}