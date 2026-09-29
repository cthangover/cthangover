using System;
using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;

namespace Cthangover.Validator
{
    /// <summary>
    /// Pure .NET file access used by validation strategies. Works with both
    /// folder and zip providers through <see cref="IModFileProvider"/> and
    /// resolves the project's <c>${include}</c> directives with the same public
    /// helper the runtime resolvers use, so the validator sees exactly the JSON
    /// the game will see. Safe on the background validation thread because it
    /// touches neither Godot nor the resolver caches.
    /// </summary>
    public static class ModFileReader
    {
        /// <summary>
        /// Lists files under a mod-relative directory. Directories are pushed
        /// onto the work stack only when <paramref name="recursive"/> is set, so
        /// the caller can mirror the recursive entity scan or the recursive
        /// scene scan.
        /// </summary>
        public static List<string> ListFiles(IModFileProvider provider, string directory, bool recursive)
        {
            var result = new List<string>();
            if (provider == null)
                return result;

            var pending = new Stack<string>();
            pending.Push(directory ?? string.Empty);

            while (pending.Count > 0)
            {
                var current = pending.Pop();
                foreach (var entry in provider.ListFiles(current))
                {
                    if (entry.EndsWith("/"))
                    {
                        if (recursive)
                            pending.Push(entry.TrimEnd('/'));
                        continue;
                    }

                    result.Add(entry);
                }
            }

            return result;
        }

        /// <summary>
        /// Reads a mod file and expands every <c>${path}</c> include. Returns
        /// null when the file is absent; include failures propagate as
        /// <see cref="InvalidOperationException"/> so the caller can report them
        /// with the owning file name.
        /// </summary>
        public static string ReadResolved(IModFileProvider provider, string path)
        {
            var content = provider.ReadFileText(path);
            if (content == null)
                return null;

            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path };
            return ModResolutionContext.ExpandModIncludes(provider, content, path, visited);
        }

        /// <summary>True when the file path ends with one of the given extensions.</summary>
        public static bool HasExtension(string path, HashSet<string> extensions)
        {
            var dot = path.LastIndexOf('.');
            return dot > 0 && extensions.Contains(path.Substring(dot));
        }
    }
}
