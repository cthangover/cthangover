namespace Cthangover.Mods.TestIntegrations.Tests
{
    using System;
    using System.Linq;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Providers;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Mods.Testing;

    public sealed class ArchiveFixtureTests
    {
        [ModTest("fixtures/zip_mount_and_includes")]
        public static void Archive(ModTestContext context)
        {
            const string id = "test_mod";
            var manager = ModManager.Instance;
            var mod = manager.GetMod(id);
            ModAssert.NotNull(mod);
            var archive = mod.FileProvider as ZipModFileProvider;
            ModAssert.NotNull(archive);
            var path = archive.RootPath.Replace('\\', '/');
            ModAssert.True(path.EndsWith("/" + ModRootFolders.TestFixtures + "/test_mod.zip", StringComparison.Ordinal));
            ModAssert.Equal("Test Mod", mod.Name);
            ModAssert.Equal("Test Author", mod.Author);
            ModAssert.True(manager.ListFiles(id).Any());
            ModAssert.True(manager.FileExists(id, "manifest.json"));
            ModAssert.True(manager.ReadFileBinary(id, "manifest.json").Length > 0);
            ModAssert.True(manager.ReadJson<ModManifest>(id, "manifest.json").Fixture);
            ModAssert.True(archive.GetFileSystemPath("manifest.json") == null);

            var included = manager.ReadResolvedText(id, "include/characters.json");
            ModAssert.True(included.Contains("100") && !included.Contains("${"));
            ModAssert.True(ScenarioDefs.Resolve("test_scenario").Contains("Hello from scenario"));
            ModAssert.True(ScenarioDefs.Resolve("with_include").Contains("100"));
        }
    }
}
