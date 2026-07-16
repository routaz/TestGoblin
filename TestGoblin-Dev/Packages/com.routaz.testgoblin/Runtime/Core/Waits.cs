using System;
using System.Threading.Tasks;


namespace Routaz.TestGoblin.Runtime.Core
{
    public static class Waits
    {
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