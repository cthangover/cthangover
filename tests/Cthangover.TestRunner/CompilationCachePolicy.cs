namespace Cthangover.TestRunner
{
    /// <summary>Chooses the persistent or cold-run compilation cache and expires unused artifact pairs under their OS leases.</summary>
    public static class CompilationCachePolicy
    {
        /// <summary>Resolves a run's assembly directory; persistent data is outside every disposable run workspace.</summary>
        public static string Resolve(string temporaryRoot, string runId, bool fresh)
            => Path.Combine(temporaryRoot, "cthangover-tests", fresh ? runId : "cache", fresh ? "cache" : "", "assemblies");

        /// <summary>Prunes pairs unused for fourteen days, skipping active compilation leases and OS-locked artifacts.</summary>
        public static int Prune(string assemblies, DateTime now)
        {
            var count = 0;
            foreach (var root in new[] { assemblies, Path.Combine(Path.GetDirectoryName(assemblies), "test_assemblies") })
            {
                if (!Directory.Exists(root)) continue;
                foreach (var path in Directory.EnumerateFiles(root, "*.dll"))
                {
                    try
                    {
                        if (!Expired(path, now)) continue;
                        using var lease = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                        if (!Expired(path, now)) continue;
                        File.Delete(path);
                        File.Delete(Path.ChangeExtension(path, ".pdb"));
                        File.Delete(path + ".used");
                        count++;
                    }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                }
            }
            return count;
        }

        private static bool Expired(string path, DateTime now)
        {
            var marker = File.Exists(path + ".used") ? path + ".used" : path;
            return File.GetLastWriteTimeUtc(marker) < now.AddDays(-14);
        }
    }
}
