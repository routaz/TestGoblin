using System;
using System.Threading.Tasks;


namespace Routaz.TestGoblin.Runtime.Core
{
    public interface IGameDriver
    {
        Task Connect(string deviceId, string altTesterHost, int altTesterPort);
        Task Disconnect();
        Task<GoblinObject> WaitForObject(string name, int id = 0);
        Task WaitForObjectGone(string name, int id = 0);
        Task WaitForCondition(Func<Task<bool>> condition, float timeout, int pollIntervalMs, string description);
        Task<GoblinObject> FindObject(string name, int id = 0);
        Task Tap(GoblinObject goblinObject);
        Task Drag(GoblinObject sourceObject, GoblinObject destinationObject);
    }
}
