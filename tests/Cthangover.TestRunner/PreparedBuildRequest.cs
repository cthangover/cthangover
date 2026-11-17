namespace Cthangover.TestRunner
{
    /// <summary>Requests verified build admission inside exclusive preparation; null requests are only pure fixture preparation.</summary>
    public sealed class PreparedBuildRequest
    {
        /// <summary>Requires the compiled unit assembly and its dependencies in addition to the game core.</summary>
        public bool IncludeUnit
        {
            get; set;
        }

        /// <summary>Refuses missing/stale receipts or outputs instead of executing a build.</summary>
        public bool NoBuild
        {
            get; set;
        }

        /// <summary>Unique run output root for build/toolchain logs; never a prepared input directory.</summary>
        public string ArtifactsDirectory
        {
            get; set;
        }
    }
}
