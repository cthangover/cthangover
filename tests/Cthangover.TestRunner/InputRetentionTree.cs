namespace Cthangover.TestRunner
{
    /// <summary>Walks owned metadata without reading payload bytes or checking every file's ancestors repeatedly.</summary>
    internal sealed class InputRetentionTree
    {
        private readonly InputRetentionBudget budget;
        private readonly InputRetentionDecision decision;
        private readonly string store;

        internal InputRetentionTree(string store, InputRetentionBudget budget, InputRetentionDecision decision)
        {
            this.store = store;
            this.budget = budget;
            this.decision = decision;
        }

        internal bool GuardDirectory(string directory)
        {
            var current = Path.GetFullPath(directory);
            while (current != null)
            {
                if (!budget.Take(decision))
                {
                    return false;
                }
                decision.AttributeReads++;
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                {
                    throw new IOException("Linked retention path refused: " + current);
                }
                current = Path.GetDirectoryName(current);
            }
            return true;
        }

        internal bool Measure(string directory, out long bytes)
        {
            bytes = 0;
            if (!GuardDirectory(directory) || !budget.Take(decision))
            {
                return false;
            }
            using var entries = Entries(directory).GetEnumerator();
            while (budget.Take(decision))
            {
                if (!entries.MoveNext())
                {
                    return true;
                }
                var entry = entries.Current;
                decision.EntriesVisited++;
                RejectEntryLink(entry);
                if ((entry.Attributes & FileAttributes.Directory) != 0)
                {
                    if (!Measure(entry.FullName, out var nested))
                    {
                        return false;
                    }
                    bytes += nested;
                }
                else
                {
                    bytes += ((FileInfo)entry).Length;
                }
            }
            return false;
        }

        internal bool Delete(string directory, bool root = false)
        {
            if (!GuardDirectory(directory) || !budget.Take(decision))
            {
                return false;
            }
            using (var entries = Entries(directory).GetEnumerator())
            {
                while (true)
                {
                    if (!budget.Take(decision))
                    {
                        return false;
                    }
                    if (!entries.MoveNext())
                    {
                        break;
                    }
                    var entry = entries.Current;
                    decision.EntriesVisited++;
                    RejectEntryLink(entry);
                    if (root && entry.Name is "origin.json" or "revision.lock" or "retiring")
                    {
                        continue;
                    }
                    if ((entry.Attributes & FileAttributes.Directory) != 0)
                    {
                        if (!Delete(entry.FullName))
                        {
                            return false;
                        }
                    }
                    else if (!DeleteFile((FileInfo)entry))
                    {
                        return false;
                    }
                }
            }
            if (!root)
            {
                if (!GuardDirectory(directory) || !budget.Take(decision))
                {
                    return false;
                }
                Directory.Delete(directory);
                decision.DirectoriesRemoved++;
                budget.Observe("directory-deleted", store, decision);
            }
            return true;
        }

        internal bool DeleteFile(FileInfo entry, bool control = false)
        {
            var bytes = entry.Length;
            if (!budget.Take(decision))
            {
                return false;
            }
            decision.AttributeReads++;
            var attributes = File.GetAttributes(entry.FullName);
            if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Directory)) != 0)
            {
                throw new IOException("Retention entry changed type or became linked: " + entry.FullName);
            }
            if ((attributes & FileAttributes.ReadOnly) != 0)
            {
                if (!budget.Take(decision))
                {
                    return false;
                }
                File.SetAttributes(entry.FullName, attributes & ~FileAttributes.ReadOnly);
                decision.AttributeWrites++;
            }
            if (!budget.Take(decision))
            {
                return false;
            }
            File.Delete(entry.FullName);
            decision.FilesRemoved++;
            decision.BytesRemoved += bytes;
            if (control)
            {
                decision.ControlRecordsRemoved++;
            }
            budget.Observe(control ? "control-deleted" : "file-deleted", store, decision);
            return true;
        }

        internal bool GuardEntry(string path)
        {
            if (!budget.Take(decision))
            {
                return false;
            }
            decision.AttributeReads++;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Linked retention entry refused: " + path);
            }
            return true;
        }

        private static IEnumerable<FileSystemInfo> Entries(string directory)
        {
            return new DirectoryInfo(directory).EnumerateFileSystemInfos("*", new EnumerationOptions
            {
                AttributesToSkip = 0,
                IgnoreInaccessible = false,
                RecurseSubdirectories = false,
                ReturnSpecialDirectories = false
            });
        }

        private static void RejectEntryLink(FileSystemInfo entry)
        {
            if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("Linked retention entry refused: " + entry.FullName);
            }
        }
    }
}
