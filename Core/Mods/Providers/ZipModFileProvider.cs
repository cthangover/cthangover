using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods.Providers
{
    public class ZipModFileProvider : IModFileProvider
    {
        private readonly ZipArchive archive;
        private readonly string zipPath;
        private readonly Dictionary<string, ZipArchiveEntry> entryLookup;
        
        public string Mod { get; }
        
        public ZipModFileProvider(string zipPath, string modName)
        {
            this.zipPath = zipPath;
            this.Mod = modName;
            archive = ZipFile.OpenRead(zipPath);
            entryLookup = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in archive.Entries)
            {
                var name = e.FullName.TrimEnd('/');
                if (name.Length > 0)
                    entryLookup[name] = e;
            }

            GameLogger.Log("MODS_ZIP", $"Opened zip '{Path.GetFileName(zipPath)}': {entryLookup.Count} entries total");
            foreach (var e in archive.Entries)
                GameLogger.Log("MODS_ZIP", $"  entry: '{e.FullName}' length={e.Length}", LogLevel.Debug);
        }

        public void Dispose()
        {
            archive?.Dispose();
        }

        public bool FileExists(string path)
        {
            var normalized = NormalizePath(path);
            var result = entryLookup.ContainsKey(normalized);
            GameLogger.Log("MODS_ZIP", $"FileExists('{path}') norm='{normalized}' => {result}", LogLevel.Debug);
            return result;
        }

        public IEnumerable<string> ListFiles(string directory = "")
        {
            var prefix = NormalizePath(directory);
            if (prefix.Length > 0 && !prefix.EndsWith("/"))
                prefix += "/";

            var seen = new HashSet<string>();
            var yieldList = new List<string>();

            foreach (var entry in archive.Entries)
            {
                var fullName = entry.FullName.TrimEnd('/');
                var isDir = fullName.Length > 0 && entry.FullName.EndsWith("/");
                if (string.IsNullOrEmpty(prefix) || fullName.StartsWith(prefix))
                {
                    var suffix = string.IsNullOrEmpty(prefix)
                        ? fullName
                        : fullName.Substring(prefix.Length);

                    if (string.IsNullOrEmpty(suffix))
                        continue;

                    var idx = suffix.IndexOf('/');
                    if (idx >= 0)
                    {
                        var dirPart = prefix + suffix.Substring(0, idx + 1);
                        if (seen.Add(dirPart))
                            yieldList.Add(dirPart);
                    }
                    else if (isDir)
                    {
                        var dirPart = prefix + suffix + "/";
                        if (seen.Add(dirPart))
                            yieldList.Add(dirPart);
                    }
                    else
                    {
                        var filePath = prefix + suffix;
                        if (seen.Add(filePath))
                            yieldList.Add(filePath);
                    }
                }
            }

            var zipName = Path.GetFileName(zipPath);
            GameLogger.Log("MODS_ZIP", $"ListFiles('{directory}') [{zipName}] prefix='{prefix}' => {yieldList.Count} entries");
            foreach (var y in yieldList)
                GameLogger.Log("MODS_ZIP", $"  -> '{y}'", LogLevel.Debug);

            foreach (var item in yieldList)
                yield return item;
        }

        public string ReadFileText(string path)
        {
            var normalized = NormalizePath(path);
            if (!entryLookup.TryGetValue(normalized, out var entry))
                return null;
            GameLogger.Log("MODS_ZIP", $"ReadFileText('{path}') norm='{normalized}' => found ({entry.Length} bytes)");
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        }

        public byte[] ReadFileBinary(string path)
        {
            var normalized = NormalizePath(path);
            if (!entryLookup.TryGetValue(normalized, out var entry))
                return null;

            GameLogger.Log("MODS_ZIP", $"ReadFileBinary('{path}') norm='{normalized}' => found ({entry.Length} bytes)");

            using var stream = entry.Open();
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }

        public Stream OpenStream(string path)
        {
            var normalized = NormalizePath(path);
            if (!entryLookup.TryGetValue(normalized, out var entry))
                return null;
            
            GameLogger.Log("MODS_ZIP", $"OpenStream('{path}') norm='{normalized}' => found");
            return entry.Open();
        }

        public string GetFileSystemPath(string path)
        {
            return null;
        }

        public string ResolveIncludePath(string includePath, string currentPath)
        {
            if (string.IsNullOrEmpty(currentPath))
                return includePath;

            var lastSlash = currentPath.LastIndexOf('/');
            var baseDir = lastSlash < 0 ? "" : currentPath.Substring(0, lastSlash);
            var result = string.IsNullOrEmpty(baseDir) ? includePath : baseDir + "/" + includePath;
            GameLogger.Log("MODS_ZIP", $"ResolveIncludePath(include='{includePath}', current='{currentPath}') baseDir='{baseDir}' => '{result}'");
            return result;
        }

        private static string NormalizePath(string path)
        {
            return path?.Replace('\\', '/').TrimStart('/') ?? "";
        }
    }
}
