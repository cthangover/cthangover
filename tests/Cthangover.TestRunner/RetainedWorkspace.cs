namespace Cthangover.TestRunner
{
    /// <summary>A locked inactive copy and its validated user-data roots, held until the retention decision is complete.</summary>
    internal sealed class RetainedWorkspace : IDisposable
    {
        internal string Root;
        internal WorkspaceOwnerInfo Owner;
        internal FileStream Lease;
        internal long Bytes;
        internal List<string> UserRoots = new();

        public void Dispose() => Lease.Dispose();
    }
}
