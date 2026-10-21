namespace Cthangover.Core.Mods
{
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// What counts as a folder mod's content. Hidden entries — a name starting
    /// with <c>.</c> (<c>.git/</c>, <c>.godot/</c>, <c>.gitignore</c>) — are not:
    /// every mod folder is its own git repository, so a walk that descends
    /// into <c>.git/</c> visits several times more entries than the mod has
    /// files. The rule is by name, not by the OS hidden attribute, which a
    /// checkout does not set reliably.
    /// </summary>
    public static class ModContentFiles
    {
        /// <summary>True for a file or directory name that is not mod content.</summary>
        public static bool IsHidden(string name) => name.StartsWith('.');

        /// <summary>Full paths of every content file under <paramref name="root"/>; hidden directories are pruned, not walked.</summary>
        public static IEnumerable<string> Enumerate(string root)
        {
            var pending = new Stack<DirectoryInfo>();
            pending.Push(new DirectoryInfo(root));
            while (pending.Count > 0)
            {
                foreach (var entry in pending.Pop().EnumerateFileSystemInfos())
                {
                    if (IsHidden(entry.Name))
                        continue;
                    if (entry is DirectoryInfo directory)
                        pending.Push(directory);
                    else
                        yield return entry.FullName;
                }
            }
        }
    }
}
