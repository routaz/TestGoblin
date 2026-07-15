using NUnit.Framework;

namespace Routaz.TestGoblin.Tests
{
    public class SanityTests
    {
        [Test]
        public void Framework_TestAssembly_IsDiscovered()
        {
            Assert.Pass("TestGoblin test assembly is wired correctly.");
        }
    }
}