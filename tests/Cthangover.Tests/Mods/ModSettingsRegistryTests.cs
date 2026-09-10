using System;
using System.IO;
using System.Reflection;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;

namespace Engine.Tests.Mods
{
    public class ModSettingsRegistryTests : IDisposable
    {
        private const string ModSettingsDir = "mod_settings";

        private static readonly System.Type RegisteredType =
            typeof(ModSettingsRegistry).GetNestedType("RegisteredModSettings",
                BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly FieldInfo EntriesField =
            typeof(ModSettingsRegistry).GetField("_entries",
                BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly FieldInfo RegistryInitializedField =
            typeof(ModRegistry).GetField("initialized",
                BindingFlags.NonPublic | BindingFlags.Instance);

        public void Dispose()
        {
            try { if (Directory.Exists(ModSettingsDir)) Directory.Delete(ModSettingsDir, true); }
            catch { }
        }

        private static void InitRegistry()
        {
            var registry = ModRegistry.Instance;
            if (!(bool)RegistryInitializedField.GetValue(registry))
                RegistryInitializedField.SetValue(registry, true);
        }

        private static void SetEntries(params (IModSettings instance, string modId)[] entries)
        {
            var list = (System.Collections.IList)EntriesField.GetValue(null);
            list.Clear();
            foreach (var (instance, modId) in entries)
            {
                var reg = System.Activator.CreateInstance(RegisteredType);
                RegisteredType.GetField("Instance").SetValue(reg, instance);
                RegisteredType.GetField("ModId").SetValue(reg, modId);
                list.Add(reg);
            }
        }

        // ─── Definitions ──────────────────────────────────────────

        [Fact]
        public void GetDefinitions_returns_definitions_from_registered_mod()
        {
            var def = new ModSettingDefinition
            {
                Key = "volume",
                Name = "Volume",
                Type = SettingType.Integer,
                DefaultValue = "50"
            };
            var settings = new Mocks.MockModSettings(
                new System.Collections.Generic.List<ModSettingDefinition> { def });
            SetEntries((settings, "audio_mod"));

            var result = ModSettingsRegistry.GetDefinitions("audio_mod");

            Assert.Single(result);
            Assert.Equal("volume", result[0].Key);
            Assert.Equal("Volume", result[0].Name);
            Assert.Equal(SettingType.Integer, result[0].Type);
            Assert.Equal("50", result[0].DefaultValue);
        }

        [Fact]
        public void GetDefinitions_unknown_mod_returns_empty()
        {
            SetEntries();
            Assert.Empty(ModSettingsRegistry.GetDefinitions("unknown"));
        }

        // ─── Write/Apply roundtrip in-memory ───────────────────────

        [Fact]
        public void WriteCurrentValues_then_ApplyValues_roundtrip_preserves_data()
        {
            SetEntries((new Mocks.MockModSettings(), "test_mod"));

            var saved = ModSettingsRegistry.WriteCurrentValues("test_mod");
            Assert.Equal(42, saved.GetInt("test_int"));
            Assert.Equal("hello", saved.GetString("test_str"));

            saved.SetInt("test_int", 99);
            saved.SetString("test_str", "updated");

            var reader = new Mocks.MockModSettings();
            SetEntries((reader, "test_mod"));
            ModSettingsRegistry.ApplyValues("test_mod", saved);

            Assert.True(reader.ReadValuesCalled);
            Assert.Equal(99, reader.LastReadBlob.GetInt("test_int"));
            Assert.Equal("updated", reader.LastReadBlob.GetString("test_str"));
        }

        [Fact]
        public void WriteCurrentValues_unknown_mod_returns_empty_blob()
        {
            SetEntries();
            var blob = ModSettingsRegistry.WriteCurrentValues("unknown");
            Assert.NotNull(blob);
            Assert.Equal(1, blob.Version);
        }

        [Fact]
        public void ApplyValues_null_blob_skips_read()
        {
            var settings = new Mocks.MockModSettings();
            SetEntries((settings, "test_mod"));

            ModSettingsRegistry.ApplyValues("test_mod", null);

            Assert.False(settings.ReadValuesCalled);
        }

        [Fact]
        public void ApplyValues_unknown_mod_does_not_crash()
        {
            SetEntries();
            ModSettingsRegistry.ApplyValues("unknown", new DataBlob());
        }

        // ─── File I/O: Save / Load / Delete ────────────────────────

        [Fact]
        public void SaveModSettings_then_LoadModSettings_roundtrip_preserves_data()
        {
            var blob = new DataBlob();
            blob.SetInt("volume", 80);
            blob.SetString("mode", "fast");
            blob.SetBool("enabled", true);

            ModSettingsRegistry.SaveModSettings("test_mod", blob);

            var loaded = ModSettingsRegistry.LoadModSettings("test_mod");
            Assert.Equal(80, loaded.GetInt("volume"));
            Assert.Equal("fast", loaded.GetString("mode"));
            Assert.True(loaded.GetBool("enabled"));
        }

        [Fact]
        public void LoadModSettings_no_file_returns_empty_blob()
        {
            var blob = ModSettingsRegistry.LoadModSettings("nonexistent_mod");
            Assert.NotNull(blob);
            Assert.Empty(blob.GetBytes());
        }

        [Fact]
        public void DeleteModSettings_removes_file()
        {
            var blob = new DataBlob();
            blob.SetInt("x", 1);
            ModSettingsRegistry.SaveModSettings("deletable_mod", blob);

            ModSettingsRegistry.DeleteModSettings("deletable_mod");

            var loaded = ModSettingsRegistry.LoadModSettings("deletable_mod");
            Assert.Empty(loaded.GetBytes());
        }

        [Fact]
        public void SaveModSettings_overwrites_existing_file()
        {
            var blob1 = new DataBlob();
            blob1.SetInt("x", 1);
            ModSettingsRegistry.SaveModSettings("overwrite_mod", blob1);

            var blob2 = new DataBlob();
            blob2.SetInt("x", 99);
            ModSettingsRegistry.SaveModSettings("overwrite_mod", blob2);

            var loaded = ModSettingsRegistry.LoadModSettings("overwrite_mod");
            Assert.Equal(99, loaded.GetInt("x"));
        }

        [Fact]
        public void LoadAll_applies_saved_settings_to_registered_mods()
        {
            var blob = new DataBlob();
            blob.SetInt("x", 55);
            ModSettingsRegistry.SaveModSettings("loadall_mod", blob);

            var settings = new Mocks.MockModSettings();
            SetEntries((settings, "loadall_mod"));

            ModSettingsRegistry.LoadAll();

            Assert.True(settings.ReadValuesCalled);
            Assert.Equal(55, settings.LastReadBlob.GetInt("x"));
        }

        // ─── GetModsWithSettings ───────────────────────────────────

        [Fact]
        public void GetModsWithSettings_returns_registered_mod_ids_sorted_by_title()
        {
            InitRegistry();
            SetEntries(
                (new Mocks.MockModSettings(), "mod_b"),
                (new Mocks.MockModSettings(), "mod_a")
            );

            var ids = ModSettingsRegistry.GetModsWithSettings();

            Assert.Equal(2, ids.Count);
            Assert.Contains("mod_a", ids);
            Assert.Contains("mod_b", ids);
        }
    }
}
