using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Engine.Tests.Arch
{
    /// <summary>
    /// Repository walking helpers shared by the path-portability tests:
    /// locating the repo root, enumerating project files outside tool and
    /// vendor folders, and checking a relative path with exact casing.
    /// </summary>
    internal static class RepoPaths
    {
        /// <summary>
        /// Folders that hold tooling, caches, build output (releases/) or vendored code rather than project assets.
        /// Agent folders (.claude, .dsh) also hold symlinks made in WSL, which Windows cannot open; jobs/ is the
        /// host job bridge, whose results carry the host's absolute paths.
        /// </summary>
        private static readonly HashSet<string> SkippedDirectories = new(StringComparer.Ordinal)
        {
            ".git", ".godot", ".idea", ".opencode", ".omo", ".dsh", ".claude", "jobs", "node_modules", "bin", "obj",
            "sdk", "site", "mockups", "gen", "artifacts", "releases", "source_code", "undefined",
        };

        public static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
                dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Repository root (AGENTS.md) not found");
        }

        /// <summary>Repo-relative, '/'-separated paths of every project file.</summary>
        public static List<string> ProjectFiles(string root)
        {
            var result = new List<string>();
            var pending = new Stack<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var dir = pending.Pop();
                foreach (var sub in Directory.EnumerateDirectories(dir))
                {
                    if (!SkippedDirectories.Contains(Path.GetFileName(sub)))
                        pending.Push(sub);
                }

                result.AddRange(Directory.EnumerateFiles(dir)
                    .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/')));
            }

            return result;
        }

        public static bool ExistsWithExactCase(string root, string relative)
        {
            var current = root;
            foreach (var segment in relative.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Directory.Exists(current))
                    return false;
                var match = Directory.EnumerateFileSystemEntries(current)
                    .FirstOrDefault(e => string.Equals(Path.GetFileName(e), segment, StringComparison.Ordinal));
                if (match == null)
                    return false;
                current = match;
            }
            return true;
        }

        public static bool ExistsIgnoringCase(string root, string relative)
        {
            var current = root;
            foreach (var segment in relative.Split('/', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!Directory.Exists(current))
                    return false;
                var match = Directory.EnumerateFileSystemEntries(current)
                    .FirstOrDefault(e => string.Equals(Path.GetFileName(e), segment, StringComparison.OrdinalIgnoreCase));
                if (match == null)
                    return false;
                current = match;
            }
            return true;
        }
    }
}
