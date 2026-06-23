using System;
using System.Collections.Generic;
using System.IO;

namespace Cthangover.Core.Mods
{
    public interface IModFileProvider : IDisposable
    {
        string Mod { get; }
        bool FileExists(string path);
        IEnumerable<string> ListFiles(string directory = "");
        string ReadFileText(string path);
        byte[] ReadFileBinary(string path);
        Stream OpenStream(string path);
        string GetFileSystemPath(string path);
        string ResolveIncludePath(string includePath, string currentPath);
    }
}
