namespace Cthangover.Tests.Mods.Compilation
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Compilation;

    /// <summary>
    /// Cache keys and the cache policy of compiled mod assemblies, including
    /// the dependency artifact that makes dependents recompile after a
    /// dependency changed.
    /// </summary>
    [Collection("mod-load-context")]
    public class ModAssemblyCacheTests : IDisposable
    {
        private readonly string depId = "test_cache_dep_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        private readonly string dir = Path.Combine(Path.GetTempPath(), "cthangover_cache_test_" + Guid.NewGuid().ToString("N"));

        public ModAssemblyCacheTests() => Directory.CreateDirectory(dir);

        public void Dispose()
        {
            ModAssemblyCache.CurrentArtifacts.TryRemove(depId, out _);
            Directory.Delete(dir, recursive: true);
        }

        private ModInfo Mod(string id, params string[] depends)
        {
            var manifest = new ModManifest { Id = id, Name = id, Version = "1.0.0", Depends = new List<ModDependency>() };
            foreach (var dependency in depends)
                manifest.Depends.Add(ModDependency.Parse(dependency));
            return new ModInfo { Id = id, Name = id, Manifest = manifest };
        }

        [Fact]
        public void BuildDependencySignature_IdsVersionsAndCurrentArtifacts()
        {
            var app = Mod("app", depId + ">=1", "missing_mod>=1");
            var mods = new Dictionary<string, IModInfo> { [depId] = Mod(depId), ["app"] = app };

            Assert.Equal(string.Empty, ModAssemblyCache.BuildDependencySignature(Mod("alone"), mods));
            Assert.Equal($"missing_mod@missing;{depId}@1.0.0", ModAssemblyCache.BuildDependencySignature(app, mods));

            ModAssemblyCache.CurrentArtifacts[depId] = Path.Combine(dir, depId + "_abc123.dll");

            Assert.Equal($"missing_mod@missing;{depId}@1.0.0#{depId}_abc123", ModAssemblyCache.BuildDependencySignature(app, mods));
        }

        [Fact]
        public void BuildCachePath_DependsOnSourcesAndDependencySignature()
        {
            var sources = new List<(string, string)> { ("A.cs", "class A { }") };

            var plain = ModAssemblyCache.BuildCachePath("app", sources, "dep@1.0.0", generatorsAvailable: true);

            Assert.Equal(plain, ModAssemblyCache.BuildCachePath("app", sources, "dep@1.0.0", generatorsAvailable: true));
            Assert.NotEqual(plain, ModAssemblyCache.BuildCachePath("app", sources, "dep@1.0.0#dep_new", generatorsAvailable: true));
            Assert.NotEqual(plain, ModAssemblyCache.BuildCachePath("app", new List<(string, string)> { ("A.cs", "class B { }") }, "dep@1.0.0", generatorsAvailable: true));
            Assert.StartsWith("app_", Path.GetFileName(plain));
        }

        [Fact]
        public void IsUsableHit_NeedsTheDllAndItsPdb()
        {
            var dll = Path.Combine(dir, "app_0123abcd.dll");
            Assert.False(ModAssemblyCache.IsUsableHit(dll));

            File.WriteAllBytes(dll, new byte[] { 1 });
            Assert.False(ModAssemblyCache.IsUsableHit(dll));
            Assert.False(File.Exists(dll));

            File.WriteAllBytes(dll, new byte[] { 1 });
            File.WriteAllBytes(Path.ChangeExtension(dll, ".pdb"), new byte[] { 2 });
            Assert.True(ModAssemblyCache.IsUsableHit(dll));
        }
    }
}
