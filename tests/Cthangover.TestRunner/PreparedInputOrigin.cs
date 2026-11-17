namespace Cthangover.TestRunner
{
    /// <summary>Self-marked store ownership makes scoped retention distinguish cache generations from arbitrary temp folders.</summary>
    public sealed class PreparedInputOrigin
    {
        /// <summary>Version of the owned preparation directory layout.</summary>
        public int SchemaVersion { get; set; } = 1;

        /// <summary>Canonical source path used only for provenance and validation, never as a deletion target.</summary>
        public string Source
        {
            get; set;
        }

        /// <summary>Path-derived identity of this source's bounded prepared-generation slot.</summary>
        public string Identity
        {
            get; set;
        }

        /// <summary>Owned project root strictly inside this store.</summary>
        public string Root
        {
            get; set;
        }
    }
}
