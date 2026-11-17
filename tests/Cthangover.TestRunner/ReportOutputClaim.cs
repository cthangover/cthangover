namespace Cthangover.TestRunner
{
    /// <summary>Prevents simultaneous extra --json aliases from replacing one another while preserving unique per-run reports.</summary>
    internal sealed class ReportOutputClaim : IDisposable
    {
        private readonly FileStream claim;

        internal ReportOutputClaim(string path)
        {
            if (path == null)
            {
                return;
            }

            path = Path.GetFullPath(path);
            PreparedInputFiles.RejectLink(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            try
            {
                claim = new FileStream(path + ".runner.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException error)
            {
                throw new IOException("extra-json-output-busy: " + path, error);
            }
        }

        /// <summary>Releases only the extra-report alias claim; unique invocation reports remain retained.</summary>
        public void Dispose()
        {
            claim?.Dispose();
        }
    }
}
