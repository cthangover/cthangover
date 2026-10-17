namespace Cthangover.Tests.Mods
{
    using System;
    using System.IO;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Providers;

    /// <summary>The install hash is reused while a mod's files are unchanged and recomputed after any change.</summary>
    public class InstallHashCacheTests : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "cthangover_hash_" + Guid.NewGuid().ToString("N"));

        public InstallHashCacheTests()
        {
            Directory.CreateDirectory(Path.Combine(root, "src"));
            File.WriteAllText(Path.Combine(root, "manifest.json"), "{ \"id\": \"m\" }");
            File.WriteAllText(Path.Combine(root, "src", "A.cs"), "class A { }");
        }

        public void Dispose() => Directory.Delete(root, recursive: true);

        [Fact]
        public void Get_MatchesTheFullHash_AndFollowsChanges()
        {
            var provider = new FolderModFileProvider(root, "m");

            var first = InstallHashCache.Get(provider, null);
            Assert.Equal(ModHasher.ComputeInstallSha256(provider, null), first);
            Assert.Equal(first, InstallHashCache.Get(provider, null));

            File.WriteAllText(Path.Combine(root, "src", "A.cs"), "class A { int changed; }");
            var second = InstallHashCache.Get(provider, null);

            Assert.NotEqual(first, second);
            Assert.Equal(ModHasher.ComputeInstallSha256(provider, null), second);
        }

        [Fact]
        public void Stamp_ChangesWithFiles_NullForMissingRoot()
        {
            var before = InstallHashCache.Stamp(root);
            Assert.Equal(before, InstallHashCache.Stamp(root));

            File.WriteAllText(Path.Combine(root, "src", "B.cs"), "class B { }");

            Assert.NotEqual(before, InstallHashCache.Stamp(root));
            Assert.NotEqual(InstallHashCache.Stamp(root), InstallHashCache.Stamp(root, new System.Collections.Generic.List<string> { ".*" }));
            Assert.Null(InstallHashCache.Stamp(Path.Combine(root, "missing")));
        }
    }
}
