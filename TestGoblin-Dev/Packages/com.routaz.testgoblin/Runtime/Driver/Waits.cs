using System;
using System.Threading.Tasks;


namespace Routaz.TestGoblin.Runtime.Driver
{
    public static class Waits
    {
        public static async Task WaitForCondition(Func<Task<bool>> condition, float timeoutSeconds, int pollIntervalMs, string description)
        {
            float elapsed = 0f;
            while (elapsed < timeoutSeconds)
            {
                if (await condition().ConfigureAwait(false))
                {
                    return;
                }
                await Task.Delay(pollIntervalMs).ConfigureAwait(false);
                elapsed += pollIntervalMs / 1000f;
            }
            throw new TimeoutException($"Timed out after {timeoutSeconds}s waiting for {description}.");
        }

    }
}