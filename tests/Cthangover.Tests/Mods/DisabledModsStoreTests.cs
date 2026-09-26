using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Cthangover.Core.Mods;

namespace Engine.Tests.Mods
{
    public class DisabledModsStoreTests : IDisposable
    {
        private const string TestFile = "disabled_mods.json";

        public void Dispose()
        {
            try
            {
                if (File.Exists(TestFile))
                    File.Delete(TestFile);
            }
            catch { }
        }

        [Fact]
        public void Load_NoFile_ReturnsEmpty()
        {
            if (File.Exists(TestFile)) File.Delete(TestFile);

            var result = DisabledModsStore.Load();

            Assert.Empty(result);
        }

        [Fact]
        public void Load_ValidJson_ReturnsSet()
        {
            var data = new List<string> { "mod_a", "mod_b" };
            File.WriteAllText(TestFile, JsonSerializer.Serialize(data));

            var result = DisabledModsStore.Load();

            Assert.Equal(2, result.Count);
            Assert.Contains("mod_a", result);
            Assert.Contains("mod_b", result);
        }

        [Fact]
        public void Load_InvalidJson_ReturnsEmpty()
        {
            File.WriteAllText(TestFile, "{ bad json }}}");

            var result = DisabledModsStore.Load();

            Assert.Empty(result);
        }

        [Fact]
        public void Save_ThenLoad_Roundtrip()
        {
            var original = new HashSet<string> { "mod1", "mod2" };
            DisabledModsStore.Save(original);

            var loaded = DisabledModsStore.Load();

            Assert.Equal(2, loaded.Count);
            Assert.True(loaded.SetEquals(original));
        }

        [Fact]
        public void SetEnabled_True_RemovesFromSet()
        {
            if (File.Exists(TestFile)) File.Delete(TestFile);
            DisabledModsStore.SetEnabled("mod_a", false);
            DisabledModsStore.SetEnabled("mod_b", false);

            DisabledModsStore.SetEnabled("mod_a", true);

            var set = DisabledModsStore.Load();
            Assert.DoesNotContain("mod_a", set);
            Assert.Contains("mod_b", set);
        }

        [Fact]
        public void SetEnabled_False_AddsToSet()
        {
            if (File.Exists(TestFile)) File.Delete(TestFile);
            DisabledModsStore.SetEnabled("mod_a", false);

            var set = DisabledModsStore.Load();
            Assert.Contains("mod_a", set);
        }

        [Fact]
        public void IsDisabled_ChecksSet()
        {
            if (File.Exists(TestFile)) File.Delete(TestFile);
            DisabledModsStore.SetEnabled("mod_a", false);
            DisabledModsStore.SetEnabled("mod_b", true);

            Assert.True(DisabledModsStore.IsDisabled("mod_a"));
            Assert.False(DisabledModsStore.IsDisabled("mod_b"));
        }

        [Fact]
        public void Remove_ExistingId_DropsItAndKeepsOthers()
        {
            if (File.Exists(TestFile)) File.Delete(TestFile);
            DisabledModsStore.SetEnabled("mod_a", false);
            DisabledModsStore.SetEnabled("mod_b", false);

            DisabledModsStore.Remove("mod_a");

            var set = DisabledModsStore.Load();
            Assert.DoesNotContain("mod_a", set);
            Assert.Contains("mod_b", set);
        }

        [Fact]
        public void Remove_UnknownId_LeavesSetUntouched()
        {
            if (File.Exists(TestFile)) File.Delete(TestFile);
            DisabledModsStore.SetEnabled("mod_a", false);

            DisabledModsStore.Remove("missing_mod");

            var set = DisabledModsStore.Load();
            Assert.Single(set);
            Assert.Contains("mod_a", set);
        }
    }
}
