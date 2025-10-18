using System.Text;
using Unleash.Internal;

namespace Unleash.Tests.Mock
{
    class MockFileSystem : IFileSystem
    {
        internal readonly Dictionary<string, string> _fileSystem = new();
        public Encoding Encoding => Encoding.UTF8;

        public bool FileExists(string path)
        {
            return _fileSystem.ContainsKey(path);
        }

        public Stream FileOpenRead(string path)
        {
            if (_fileSystem.TryGetValue(path, out var content))
            {
                return new TrackingWriteStream(path, _fileSystem, Encoding.UTF8);
            }
            throw new FileNotFoundException();
        }

        public Stream FileOpenCreate(string path)
        {
            return new TrackingWriteStream(path, _fileSystem, Encoding.UTF8);
        }

        public void WriteAllText(string path, string content)
        {
            _fileSystem[path] = content;
        }

        public string ReadAllText(string path)
        {
            if (!_fileSystem.TryGetValue(path, out var content))
            {
                throw new FileNotFoundException();
            }
            return content!;
        }

        public void Move(string sourcePath, string destPath)
        {
            if (_fileSystem.TryGetValue(sourcePath, out var content))
            {
                _fileSystem.Remove(sourcePath);
                _fileSystem[destPath] = content;
            }
        }

        public void Replace(string sourceFileName, string destinationFileName, string destinationBackupFileName)
        {
            if (_fileSystem.TryGetValue(sourceFileName, out var content))
            {
                _fileSystem.Remove(sourceFileName);
                _fileSystem[destinationFileName] = content;
                _fileSystem[destinationBackupFileName] = content;
            }
        }

        public void Delete(string path)
        {
            _fileSystem.Remove(path);
        }

        internal List<string> ListFiles()
        {
            return _fileSystem.Keys.ToList();
        }
    }

    class TrackingWriteStream : MemoryStream
    {
        private readonly string _path;
        private readonly Dictionary<string, string> _fs;
        private readonly Encoding _encoding;

        public TrackingWriteStream(string path, Dictionary<string, string> fs, Encoding encoding)
        {
            _path = path;
            _fs = fs;
            _encoding = encoding;
        }

        public override void Flush()
        {
            Position = 0;
            using (var reader = new StreamReader(this, _encoding, leaveOpen: true))
            {
                var content = reader.ReadToEnd();
                _fs[_path] = content;
            }

            base.Flush();
        }
    }
}
