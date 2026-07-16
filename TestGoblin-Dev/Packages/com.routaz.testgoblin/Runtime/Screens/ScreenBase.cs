using Routaz.TestGoblin.Runtime.Driver;
using System.Threading.Tasks;

namespace Routaz.TestGoblin.Runtime.Screens
{
    public abstract class ScreenBase
    {
        protected IGameDriver Driver { get; }

        /// <summary>Object whose presence proves this screen is visible.</summary>
        protected abstract string SignatureObjectName { get; }

        protected ScreenBase(IGameDriver driver)
        {
          Driver = driver;
        }

        public Task WaitUntilVisible()
        {
            return Driver.WaitForObject(SignatureObjectName);
        }

    }
}
