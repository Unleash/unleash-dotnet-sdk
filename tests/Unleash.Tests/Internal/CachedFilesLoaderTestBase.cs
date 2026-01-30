using NUnit.Framework;

namespace Unleash.Tests.Internal
{
    public class CachedFilesLoaderTestBase
    {
        protected string AppDataFile(string filename)
        {
            var file = Path.Combine(TestContext.CurrentContext.TestDirectory, "App_Data", filename);
            return file;
        }
    }
}
