using System;
using System.Collections.Generic;
using Cthangover.Core.Mods;
using Engine.Tests.Mods.Mocks;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// Integrations (<c>"integrations": [{"id", "with"}]</c>) join the filtered mod set as the virtual mod
    /// <c>&lt;host&gt;__&lt;id&gt;</c> only when every partner is in it, depending on host and partners.
    /// </summary>
    public class ModIntegrationsTests
    {
        private static readonly ModIntegration Quests = new() { Id = "quests", With = new List<string> { "quests_base" } };

        [Fact]
        public void IsWanted_NeedsEveryPartnerInTheSet()
        {
            Assert.True(ModIntegrations.IsWanted(Quests, new[] { "turn_battle_base", "QUESTS_BASE" }));
            Assert.False(ModIntegrations.IsWanted(Quests, new[] { "turn_battle_base" }));
            Assert.False(ModIntegrations.IsWanted(new ModIntegration { Id = "two", With = new List<string> { "a", "b" } }, new[] { "a" }));
            Assert.True(ModIntegrations.IsWanted(new ModIntegration { Id = "none" }, Array.Empty<string>()));
            Assert.False(ModIntegrations.IsWanted(new ModIntegration { With = new List<string> { "a" } }, new[] { "a" }));
            Assert.False(ModIntegrations.IsWanted(new ModIntegration { Id = "blank", With = new List<string> { " " } }, new[] { "a" }));
            Assert.False(ModIntegrations.IsWanted(null, new[] { "a" }));
        }

        [Fact]
        public void Mount_WithThePartner_AddsTheGlueDependingOnHostAndPartner()
        {
            var mods = Set(Host(), Plain("quests_base"));

            var mounted = ModIntegrations.Mount(mods);

            Assert.Equal(new[] { "turn_battle_base__quests" }, mounted);
            var glue = (ModInfo)mods["turn_battle_base__quests"];
            Assert.Equal(new[] { "turn_battle_base", "quests_base" }, glue.Manifest.DependencyIds);
            Assert.Equal("2.0.0", glue.Manifest.Version);
            Assert.Null(glue.Manifest.Integrations);
            Assert.Equal("class Glue {}", glue.FileProvider.ReadFileText("src/Glue.cs"));
        }

        [Fact]
        public void Mount_WithoutThePartner_LeavesTheSetAlone()
        {
            var mods = Set(Host());

            Assert.Empty(ModIntegrations.Mount(mods));
            Assert.Equal(new[] { "turn_battle_base" }, mods.Keys);
        }

        [Fact]
        public void Mount_PartnerDeclaredInTheGluesManifest_IsNotDuplicated()
        {
            var host = Host("""{ "depends": ["quests_base>=1"] }""");
            var mods = Set(host, Plain("quests_base"));

            ModIntegrations.Mount(mods);

            Assert.Equal(new[] { "turn_battle_base", "quests_base" }, ((ModInfo)mods["turn_battle_base__quests"]).Manifest.DependencyIds);
        }

        [Fact]
        public void Mount_MissingManifestOrIdClash_LeavesItOut()
        {
            var noFolder = new ModInfo
            {
                Id = "turn_battle_base",
                FileProvider = new MockModFileProvider("turn_battle_base"),
                Manifest = new ModManifest { Id = "turn_battle_base", Integrations = new List<ModIntegration> { Quests } },
            };
            Assert.Empty(ModIntegrations.Mount(Set(noFolder, Plain("quests_base"))));

            var installed = Plain("turn_battle_base__quests");
            var clash = Set(Host(), Plain("quests_base"), installed);
            Assert.Empty(ModIntegrations.Mount(clash));
            Assert.Same(installed, clash["turn_battle_base__quests"]);
        }

        [Fact]
        public void Manifest_ReadsIntegrationsAndTheirFolder()
        {
            var manifest = System.Text.Json.JsonSerializer.Deserialize<ModManifest>(
                """{ "id": "h", "integrations": [ { "id": "quests", "with": ["quests_base"] }, { "id": "x", "with": [], "path": "glue/x/" } ] }""");

            Assert.Equal("integrations/quests", manifest.Integrations[0].Folder);
            Assert.Equal(new[] { "quests_base" }, manifest.Integrations[0].With);
            Assert.Equal("glue/x", manifest.Integrations[1].Folder);
        }

        private static Dictionary<string, IModInfo> Set(params ModInfo[] mods)
        {
            var set = new Dictionary<string, IModInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in mods)
                set[mod.Id] = mod;
            return set;
        }

        private static ModInfo Host(string glueManifest = """{ "sources": ["src/*.cs"] }""")
        {
            var provider = new MockModFileProvider("turn_battle_base");
            provider.AddFile("integrations/quests/manifest.json", glueManifest);
            provider.AddFile("integrations/quests/src/Glue.cs", "class Glue {}");
            return new ModInfo
            {
                Id = "turn_battle_base",
                Name = "Battles",
                FileProvider = provider,
                Manifest = new ModManifest { Id = "turn_battle_base", Version = "2.0.0", Integrations = new List<ModIntegration> { Quests } },
            };
        }

        private static ModInfo Plain(string id)
            => new() { Id = id, FileProvider = new MockModFileProvider(id), Manifest = new ModManifest { Id = id } };
    }
}
