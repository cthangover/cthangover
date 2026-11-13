namespace Cthangover.TestRunner
{
    using System.Text;

    /// <summary>Parallel outer runs give xUnit one job: nested collection parallelism otherwise starves short async assertions.</summary>
    public static class UnitRunSettings
    {
        /// <summary>Persists VSTest adapter settings beside the TRX without changing the project or excluding any test.</summary>
        public static string Write(string directory)
        {
            Directory.CreateDirectory(directory);
            var path = Path.GetFullPath(Path.Combine(directory, "unit.runsettings"));
            File.WriteAllText(path, "<RunSettings><xUnit><ParallelizeTestCollections>false</ParallelizeTestCollections></xUnit></RunSettings>\n", new UTF8Encoding(false));
            return path;
        }
    }
}
