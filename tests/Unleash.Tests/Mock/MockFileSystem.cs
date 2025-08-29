using System.IO;
using System.Text;
using Unleash.Internal;

namespace Unleash.Tests.Mock
{
    class MockFileSystem : IFileSystem
    {
        public Encoding Encoding => throw new NotImplementedException();

        public bool FileExists(string path)
        {
            return true;
        }

        public Stream FileOpenRead(string path)
        {
            return new MemoryStream();
        }

        public Stream FileOpenCreate(string path)
        {
            return new MemoryStream();
        }

        public void WriteAllText(string path, string content)
        {
        }

        public string ReadAllText(string path)
        {
            return string.Empty;
        }

        public void Move(string sourcePath, string destPath)
        {
        }

        public void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName)
        {
        }

        public void Delete(string path)
        {
        }
    }
}