namespace Cthangover.TestRunner
{
    using System.Security.Cryptography;
    using System.Text;

    /// <summary>Explicit input selection excludes developer SDK executables and private Godot export/editor state.</summary>
    internal static class PreparedInputFiles
    {
        internal const string Policy = "prepared_inputs_v1_fixed_home_no_developer_sdk";

        private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase)
        {
            ".git",
            ".git-template",
            ".idea",
            ".ide",
            "node_modules",
            "__pycache__",
            "obj"
        };

        private static readonly string[] Trees = new[]
        {
            "Core", "scenes", "collections", "config", "tests", "docs", ".opencode", "sdk/fixtures"
        }.Concat(ModFolders.Names).ToArray();

        internal static List<PreparedInputFile> Scan(string root, CancellationToken cancellation)
        {
            var result = new List<PreparedInputFile>();
            foreach (var file in Directory.EnumerateFiles(root).Order(StringComparer.Ordinal))
            {
                if (Path.GetExtension(file) is ".godot" or ".csproj" or ".props" or ".targets" or ".sln" or ".cfg"
                    or ".config" or ".globalconfig" or ".json" or ".md" || Path.GetFileName(file) == ".editorconfig")
                {
                    result.Add(Read(root, file, cancellation));
                }
            }

            foreach (var tree in Trees)
            {
                var directory = System.IO.Path.Combine(root, tree);
                if (Directory.Exists(directory))
                {
                    Visit(root, directory, result, cancellation);
                }
            }

            var imported = System.IO.Path.Combine(root, ".godot", "imported");
            if (Directory.Exists(imported))
            {
                Visit(root, imported, result, cancellation);
            }

            foreach (var name in new[] { "uid_cache.bin", "global_script_class_cache.cfg", "scene_groups_cache.cfg", "extension_list.cfg" })
            {
                var file = System.IO.Path.Combine(root, ".godot", name);
                if (File.Exists(file))
                {
                    result.Add(Read(root, file, cancellation));
                }
            }

            result.Sort(Compare);
            return result;
        }

        internal static PreparedInputFile Read(string root, string file, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            RejectLink(file);
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
            var length = stream.Length;
            var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            cancellation.ThrowIfCancellationRequested();
            if (length != stream.Length)
            {
                throw new IOException("Input changed while hashing: " + file);
            }

            return new PreparedInputFile
            {
                Path = System.IO.Path.GetRelativePath(root, file).Replace('\\', '/'),
                Length = length,
                Hash = hash
            };
        }

        internal static string Fingerprint(IEnumerable<PreparedInputFile> files, string toolchain = null)
        {
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            digest.AppendData(Encoding.UTF8.GetBytes(Policy + "\n"));
            digest.AppendData(Encoding.UTF8.GetBytes((toolchain ?? "fixture") + "\n"));
            foreach (var file in files.OrderBy(PathKey, StringComparer.Ordinal))
            {
                digest.AppendData(Encoding.UTF8.GetBytes(file.Path + "\0" + file.IsDirectory + "\0"
                    + file.Length + "\0" + file.Hash + "\n"));
            }

            return Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
        }

        internal static bool Matches(string root, PreparedInputFile expected)
        {
            var path = SafePath(root, expected.Path);
            if (expected.IsDirectory)
            {
                RejectLink(path);
                return Directory.Exists(path);
            }

            if (!File.Exists(path) || new FileInfo(path).Length != expected.Length)
            {
                return false;
            }

            return Read(root, path).Hash == expected.Hash;
        }

        internal static string SafePath(string root, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || System.IO.Path.IsPathRooted(relative)
                || relative.Replace('\\', '/').Split('/').Any(InvalidPart))
            {
                throw new IOException("Unsafe prepared relative path: " + relative);
            }

            var result = System.IO.Path.GetFullPath(System.IO.Path.Combine(root, relative));
            if (!WorkspaceOwner.IsWithin(result, root))
            {
                throw new IOException("Prepared path escapes input root: " + relative);
            }

            return result;
        }

        internal static void RejectLink(string path)
        {
            var current = System.IO.Path.GetFullPath(path);
            while (current != null)
            {
                try
                {
                    if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new IOException("Linked input path refused: " + current);
                    }
                }
                catch (FileNotFoundException)
                {
                    // Future owned paths still require validating every existing ancestor.
                }
                catch (DirectoryNotFoundException)
                {
                }

                current = System.IO.Path.GetDirectoryName(current);
            }
        }

        internal static void Writable(string path)
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
            }
        }

        internal static bool Scratch(string relative)
        {
            return relative.StartsWith("obj/", StringComparison.Ordinal) || relative.Contains("/obj/", StringComparison.Ordinal)
                || relative.StartsWith(".godot/mono/temp/obj/", StringComparison.Ordinal);
        }

        private static void Visit(string root, string directory, List<PreparedInputFile> files, CancellationToken cancellation)
        {
            RejectLink(directory);
            files.Add(new PreparedInputFile
            {
                Path = System.IO.Path.GetRelativePath(root, directory).Replace('\\', '/'),
                IsDirectory = true,
                Hash = "directory"
            });
            foreach (var file in Directory.EnumerateFiles(directory).Order(StringComparer.Ordinal))
            {
                files.Add(Read(root, file, cancellation));
            }

            foreach (var child in Directory.EnumerateDirectories(directory).Order(StringComparer.Ordinal))
            {
                var name = System.IO.Path.GetFileName(child);
                var relative = System.IO.Path.GetRelativePath(root, child).Replace('\\', '/');
                if (Excluded.Contains(name) || relative == "tests/artifacts"
                    || relative.EndsWith("/bin/Debug", StringComparison.Ordinal)
                    || relative.StartsWith("tests/", StringComparison.Ordinal) && name == "bin")
                {
                    continue;
                }

                Visit(root, child, files, cancellation);
            }
        }

        private static bool InvalidPart(string part)
        {
            return part is "" or "." or ".." || part.Contains(':');
        }

        private static int Compare(PreparedInputFile left, PreparedInputFile right)
        {
            return string.CompareOrdinal(left.Path, right.Path);
        }

        private static string PathKey(PreparedInputFile file)
        {
            return file.Path;
        }
    }
}
