using NUnit.Framework;

namespace Unleash.Tests
{
    public abstract class BaseTest
    {
        protected string AppDataFile(string filename)
        {
            var file = Path.Combine(TestContext.CurrentContext.TestDirectory, "App_Data", "features-v1.json");
            return file;
        }

        [SetUp]
        public void Setup()
        {
        }
    }
}