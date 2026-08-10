using System;
using System.Threading.Tasks;


namespace Routaz.TestGoblin.Runtime.Driver
{
    /// <summary>
    /// The one abstraction every layer above the driver depends on.
    /// </summary>
    /// <remarks>
    /// Deliberately vendor-neutral. Whatever a driver needs in order to reach the
    /// game — a host and port, a device id, nothing at all — is its own business and
    /// belongs in its constructor, not in this contract. An interface that named a
    /// specific tool would make every layer above it aware of that tool, which is
    /// exactly what the seam exists to prevent.
    /// </remarks>
    public interface IGameDriver
    {
        /// <summary>Establishes whatever connection this driver needs. May be a no-op.</summary>
        Task Connect();

        /// <summary>Releases whatever <see cref="Connect"/> acquired. May be a no-op.</summary>
        Task Disconnect();
        Task<GoblinObject> WaitForObject(string name, int id = 0);
        Task WaitForObjectGone(string name, int id = 0);
        Task WaitForCondition(Func<Task<bool>> condition, float timeout, int pollIntervalMs, string description);
        Task<GoblinObject> FindObject(string name, int id = 0);
        Task Tap(GoblinObject goblinObject);
        Task Drag(GoblinObject sourceObject, GoblinObject destinationObject);
        Task<string> GetText(GoblinObject goblinObject);
    }
}
