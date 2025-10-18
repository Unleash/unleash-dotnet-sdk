using System.IO;
using System.Text;

namespace Unleash.Internal
{
    internal interface IFileSystem
    {
        Encoding Encoding { get; }
        bool FileExists(string path);
        Stream FileOpenRead(string path);
        Stream FileOpenCreate(string path);
        void WriteAllText(string path, string content);
        string ReadAllText(string path);
        void Move(string sourcePath, string destPath);
        void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName);
        void Delete(string path);
    }
}