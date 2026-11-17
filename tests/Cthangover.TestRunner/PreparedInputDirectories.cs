namespace Cthangover.TestRunner
{
    /// <summary>
    /// Complements file-content fences with directory-only mutations. Known input parents survive restoration;
    /// unknown empty leaves are observable without duplicating every parent of an already reported changed file.
    /// </summary>
    internal static class PreparedInputDirectories
    {
        internal static HashSet<string> Expected(IEnumerable<PreparedInputFile> files)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in files)
            {
                var parent = file.IsDirectory ? file.Path : Path.GetDirectoryName(file.Path)?.Replace('\\', '/');
                while (!string.IsNullOrEmpty(parent))
                {
                    result.Add(parent);
                    parent = Path.GetDirectoryName(parent)?.Replace('\\', '/');
                }
            }
            return result;
        }

        internal static void FindUnexpectedEmpty(string root, string directory, HashSet<string> expected,
            ICollection<string> changes, bool preserveScratch)
        {
            PreparedInputFiles.RejectLink(directory);
            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                PreparedInputFiles.RejectLink(child);
                var relative = Path.GetRelativePath(root, child).Replace('\\', '/');
                if (preserveScratch && PreparedInputFiles.Scratch(relative + "/"))
                {
                    continue;
                }
                if (!expected.Contains(relative) && !Directory.EnumerateFileSystemEntries(child).Any())
                {
                    changes.Add(relative);
                }
                else
                {
                    FindUnexpectedEmpty(root, child, expected, changes, preserveScratch);
                }
            }
        }

        internal static void RemoveUnexpectedEmpty(string root, string directory, HashSet<string> expected,
            bool preserveScratch)
        {
            PreparedInputFiles.RejectLink(directory);
            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                PreparedInputFiles.RejectLink(child);
                var relative = Path.GetRelativePath(root, child).Replace('\\', '/');
                if (preserveScratch && PreparedInputFiles.Scratch(relative + "/"))
                {
                    continue;
                }
                RemoveUnexpectedEmpty(root, child, expected, preserveScratch);
                if (!expected.Contains(relative) && !Directory.EnumerateFileSystemEntries(child).Any())
                {
                    Directory.Delete(child);
                }
            }
        }
    }
}
