using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Unleash.Tests.Internal
{
    public class CachedFilesLoaderTestBase
    {
        protected string AppDataFile(string filename)
        {
            var file = Path.Combine(AppDataDirectory(), filename);
            return file;
        }

        protected string AppDataDirectory()
        {
            return Path.Combine(TestContext.CurrentContext.TestDirectory, "App_Data");
        }
    }
}
