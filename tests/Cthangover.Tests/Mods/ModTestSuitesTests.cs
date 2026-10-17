using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Pipeline;
using Cthangover.Core.Mods.Providers;
using Engine.Tests.Mods.Mocks;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Test suites live inside their host mod and become the virtual mod
    /// <c>&lt;host&gt;__&lt;suite&gt;</c> only behind their activate args; fixtures
    /// stay out of <c>--all-mods</c> unless named in <c>--with-mods</c>.
    /// </summary>
    public class ModTestSuitesTests
    {
        private static readonly ModTestSuite Suite = new() { Id = "walk", Activate = new List<string> { "--itest=walk" } };

        [Fact]
        public void IsActive_OnlyWithActivatePrefixOrAllFlag()
        {
            Assert.False(ModTestSuites.IsActive(Suite, Array.Empty<string>()));
            Assert.False(ModTestSuites.IsActive(Suite, new[] { "--all-mods", "--itest=other" }));
            Assert.True(ModTestSuites.IsActive(Suite, new[] { "--itest=walk" }));
            Assert.True(ModTestSuites.IsActive(Suite, new[] { ModTestSuites.AllFlag }));

            var prefix = new ModTestSuite { Id = "autoplay", Activate = new List<string> { "--timeout=" } };
            Assert.True(ModTestSuites.IsActive(prefix, new[] { "--timeout=60" }));
            Assert.False(ModTestSuites.IsActive(new ModTestSuite { Id = "none" }, new[] { "--itest=none" }));
        }

        [Fact]
        public void CreateSuiteMod_ZipHost_DependsOnHostAndReadsSubfolder()
        {
            var host = Host(new MockModFileProvider("cooking"));
            var provider = (MockModFileProvider)host.FileProvider;
            provider.AddFile("test_suites/walk/manifest.json", @"{ ""sources"": [""src/*.cs""], ""depends"": [""dialog_base>=1""] }");
            provider.AddFile("test_suites/walk/src/Driver.cs", "class Driver {}");

            var suite = ModTestSuites.CreateSuiteMod(host, Suite);

            Assert.Equal("cooking__walk", suite.Id);
            Assert.Equal("cooking__walk", suite.Manifest.Id);
            Assert.Equal("1.2.0", suite.Manifest.Version);
            Assert.Equal(new[] { "cooking", "dialog_base" }, suite.Manifest.DependencyIds);
            Assert.Null(suite.Manifest.TestSuites);
            Assert.IsType<SubdirModFileProvider>(suite.FileProvider);
            Assert.Equal(new[] { "src/" }, suite.FileProvider.ListFiles().Where(e => e.EndsWith("/")));
            Assert.Equal("class Driver {}", suite.FileProvider.ReadFileText("src/Driver.cs"));
        }

        [Fact]
        public void CreateSuiteMod_FolderHost_UsesFolderProvider()
        {
            var root = Path.Combine(Path.GetTempPath(), "suite_" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(Path.Combine(root, "test_suites", "walk"));
                File.WriteAllText(Path.Combine(root, "test_suites", "walk", "manifest.json"), "{}");
                var host = Host(new FolderModFileProvider(root, "cooking"));

                var suite = ModTestSuites.CreateSuiteMod(host, Suite);

                Assert.IsType<FolderModFileProvider>(suite.FileProvider);
                Assert.Equal(Path.Combine(root, "test_suites", "walk"), suite.FileProvider.RootPath);
                Assert.Equal(new[] { "cooking" }, suite.Manifest.DependencyIds);
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Fact]
        public void CreateSuiteMod_MissingManifest_ReturnsNull()
        {
            Assert.Null(ModTestSuites.CreateSuiteMod(Host(new MockModFileProvider("cooking")), Suite));
        }

        [Fact]
        public void Mount_AddsOnlyActiveSuites()
        {
            var provider = new MockModFileProvider("cooking");
            provider.AddFile("test_suites/walk/manifest.json", "{}");
            provider.AddFile("test_suites/idle/manifest.json", "{}");
            var host = Host(provider, new ModTestSuite { Id = "idle", Activate = new List<string> { "--itest=idle" } });
            var mods = new Dictionary<string, IModInfo>(StringComparer.OrdinalIgnoreCase) { [host.Id] = host };

            ModTestSuites.Mount(mods, new[] { "--itest=walk" });

            Assert.Equal(new[] { "cooking", "cooking__walk" }, mods.Keys.OrderBy(k => k));
            Assert.Equal(new[] { "cooking__walk" }, ModTestSuites.ActiveIds);

            ModTestSuites.Mount(new Dictionary<string, IModInfo> { [host.Id] = host }, Array.Empty<string>());
            Assert.Empty(ModTestSuites.ActiveIds);
        }

        [Fact]
        public void RemoveFixtures_KeepsOnlyNamedFixtures()
        {
            var mods = new Dictionary<string, IModInfo>(StringComparer.OrdinalIgnoreCase)
            {
                ["game"] = new ModInfo { Id = "game", Manifest = new ModManifest { Id = "game" } },
                ["patches"] = new ModInfo { Id = "patches", Manifest = new ModManifest { Id = "patches", Fixture = true } },
            };

            ModSetFilter.RemoveFixtures(mods, Array.Empty<string>());
            Assert.Equal(new[] { "game" }, mods.Keys);

            mods["patches"] = new ModInfo { Id = "patches", Manifest = new ModManifest { Id = "patches", Fixture = true } };
            ModSetFilter.RemoveFixtures(mods, new[] { "patches" });
            Assert.Contains("patches", mods.Keys);
        }

        private static ModInfo Host(IModFileProvider provider, params ModTestSuite[] extra)
        {
            var suites = new List<ModTestSuite> { Suite };
            suites.AddRange(extra);
            return new ModInfo
            {
                Id = "cooking",
                Name = "Cooking",
                FileProvider = provider,
                Manifest = new ModManifest { Id = "cooking", Version = "1.2.0", TestSuites = suites },
            };
        }
    }
}
