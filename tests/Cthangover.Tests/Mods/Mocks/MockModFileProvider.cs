using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods.Mocks
{
    public class MockModFileProvider : IModFileProvider
    {
        private readonly Dictionary<string, string> _textFiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, byte[]> _binaryFiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _modName;

        public string Mod => _modName;

        public string RootPath => null;

        public MockModFileProvider(string modName)
        {
            _modName = modName;
        }

        public void AddFile(string path, string content)
        {
            _textFiles[path.Replace('\\', '/')] = content;
            var dir = Path.GetDirectoryName(path.Replace('\\', '/'))?.Replace('\\', '/');
            while (!string.IsNullOrEmpty(dir))
            {
                _directories.Add(dir + "/");
                var parent = Path.GetDirectoryName(dir)?.Replace('\\', '/');
                if (parent == dir) break;
                dir = parent;
            }
        }

        public void AddBinaryFile(string path, byte[] data)
        {
            _binaryFiles[path.Replace('\\', '/')] = data;
        }

        public void AddDirectory(string path)
        {
            _directories.Add(path.Replace('\\', '/').TrimEnd('/') + "/");
        }

        public bool FileExists(string path)
        {
            return _textFiles.ContainsKey(path.Replace('\\', '/'))
                || _binaryFiles.ContainsKey(path.Replace('\\', '/'));
        }

        public IEnumerable<string> ListFiles(string directory = "")
        {
            var prefix = string.IsNullOrEmpty(directory)
                ? ""
                : directory.Replace('\\', '/').TrimEnd('/') + "/";

            var result = new List<string>();

            foreach (var dir in _directories)
            {
                if (dir.StartsWith(prefix) && dir != prefix)
                {
                    var relative = dir.Substring(prefix.Length);
                    var slashIdx = relative.IndexOf('/');
                    if (slashIdx == relative.Length - 1)
                        result.Add(prefix + relative);
                }
            }

            foreach (var file in _textFiles.Keys)
            {
                if (file.StartsWith(prefix))
                {
                    var relative = file.Substring(prefix.Length);
                    if (!relative.Contains("/"))
                        result.Add(file);
                }
            }

            foreach (var file in _binaryFiles.Keys)
            {
                if (file.StartsWith(prefix))
                {
                    var relative = file.Substring(prefix.Length);
                    if (!relative.Contains("/") && !_textFiles.ContainsKey(file))
                        result.Add(file);
                }
            }

            return result;
        }

        public string ReadFileText(string path)
        {
            _textFiles.TryGetValue(path.Replace('\\', '/'), out var content);
            return content;
        }

        public byte[] ReadFileBinary(string path)
        {
            _binaryFiles.TryGetValue(path.Replace('\\', '/'), out var data);
            return data;
        }

        public Stream OpenStream(string path)
        {
            var content = ReadFileText(path);
            if (content == null)
                return null;
            return new MemoryStream(Encoding.UTF8.GetBytes(content));
        }

        public string GetFileSystemPath(string path)
        {
            return null;
        }

        public string ResolveIncludePath(string includePath, string currentPath)
        {
            var currentDir = Path.GetDirectoryName(currentPath.Replace('\\', '/'));
            if (string.IsNullOrEmpty(currentDir))
                return includePath;

            var resolved = Path.Combine(currentDir, includePath).Replace('\\', '/');
            var parts = new List<string>();
            foreach (var part in resolved.Split('/'))
            {
                if (part == "..")
                {
                    if (parts.Count > 0)
                        parts.RemoveAt(parts.Count - 1);
                }
                else if (part != ".")
                {
                    parts.Add(part);
                }
            }
            return string.Join("/", parts);
        }

        public void Dispose()
        {
        }
    }
}
