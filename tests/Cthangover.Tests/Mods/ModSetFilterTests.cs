namespace Cthangover.Tests.Mods
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Pipeline;

    /// <summary>Disabling and dependency validation of the discovered mod set before compilation.</summary>
    [Collection("disabled-mods-store")]
    public class ModSetFilterTests : IDisposable
    {
        public void Dispose() => File.Delete("disabled_mods.json");

        [Fact]
        public void FilterDisabledMods_CascadesOverRequiredDependenciesOnly()
        {
            DisabledModsStore.Save(new HashSet<string> { "voice" });
            var mods = Set(Mod("voice"), Mod("story", "voice?"), Mod("dub", "voice>=1"), Mod("menu"));

            ModSetFilter.FilterDisabledMods(mods);

            Assert.Equal(new[] { "menu", "story" }, mods.Keys.OrderBy(k => k));
        }

        [Fact]
        public void FilterDisabledMods_ExplicitModAndItsRequiredDependenciesStayEnabled()
        {
            DisabledModsStore.Save(new HashSet<string> { "fixture", "base", "voice" });
            var mods = Set(Mod("base"), Mod("fixture", "base>=1"), Mod("voice"), Mod("menu"));

            ModSetFilter.FilterDisabledMods(mods, new[] { "fixture" });

            Assert.Equal(new[] { "base", "fixture", "menu" }, mods.Keys.OrderBy(k => k));
            Assert.Equal(new[] { "base", "fixture", "voice" }, DisabledModsStore.Load().OrderBy(k => k));
        }

        [Fact]
        public void ValidateDependencies_DropsDependentsOfARemovedMod_Transitively()
        {
            var mods = Set(
                Mod("engine"),
                Mod("broken", "missing>=1"),
                Mod("content", "broken>=1"),
                Mod("addon", "content>=1"),
                Mod("game", "engine>=1"));

            ModSetFilter.ValidateDependencies(mods);

            Assert.Equal(new[] { "engine", "game" }, mods.Keys.OrderBy(k => k));
        }

        [Fact]
        public void ValidateDependencies_KeepsOptionalMissing_DropsWrongVersion()
        {
            var mods = Set(
                Mod("engine"),
                Mod("soft", "missing>=1?"),
                Mod("strict", "engine>=2"),
                Mod("pinned", "engine=1.0.0"));

            ModSetFilter.ValidateDependencies(mods);

            Assert.Equal(new[] { "engine", "pinned", "soft" }, mods.Keys.OrderBy(k => k));
        }

        private static Dictionary<string, IModInfo> Set(params ModInfo[] mods)
            => mods.ToDictionary(m => m.Id, m => (IModInfo)m);

        private static ModInfo Mod(string id, params string[] depends) => new()
        {
            Id = id,
            Manifest = new ModManifest
            {
                Id = id,
                Version = "1.0.0",
                Depends = depends.Select(ModDependency.Parse).ToList(),
            },
        };
    }
}
