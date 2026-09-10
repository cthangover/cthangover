using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;

namespace Engine.Tests.Settings
{
    public class SavePackageTests
    {
        [Fact]
        public void Serialize_then_deserialize_restores_core_slot_data()
        {
            var pkg = new SavePackage { SaveId = "slot_1", CoreVersion = "1.0.0" };

            var coreSlot = new ModSlot
            {
                Metadata = new ModMetadata
                {
                    ModId = SavePackage.CoreSlotKey,
                    ModName = "Core Game",
                    IsRequired = true,
                    MinCoreVersion = "1.0.0"
                }
            };
            coreSlot.Data["Time"] = new DataBlob(1, 12345L);
            pkg.ModData[SavePackage.CoreSlotKey] = coreSlot;

            var json = JsonSerializer.Serialize(pkg);
            var deserialized = JsonSerializer.Deserialize<SavePackage>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip });

            Assert.NotNull(deserialized);
            Assert.Equal("slot_1", deserialized.SaveId);
            Assert.Equal("1.0.0", deserialized.CoreVersion);
            Assert.True(deserialized.ModData.ContainsKey(SavePackage.CoreSlotKey));

            var restored = deserialized.ModData[SavePackage.CoreSlotKey];
            Assert.Equal(SavePackage.CoreSlotKey, restored.Metadata.ModId);
            Assert.True(restored.Data.ContainsKey("Time"));
        }
        
        [Serializable]
        class CustomData
        {
            [JsonInclude] public int Code { get; set; }
            public string Text { get; set; }
        }
        [Fact]
        public void Serialize_then_deserialize_restores_mod_slot_metadata()
        {
            var pkg = new SavePackage { SaveId = "slot_2", CoreVersion = "2.0.0" };
            var modSlot = new ModSlot
            {
                Metadata = new ModMetadata
                {
                    ModId = "test_mod",
                    ModName = "Test Mod",
                    ModVersion = "1.5.0",
                    MinCoreVersion = "2.0.0",
                    IsRequired = false
                }
            };
            
            modSlot.Data["custom"] = new DataBlob(1, new CustomData
            {
                Code = 42,
                Text = "cthangover"
            });
            pkg.ModData["test_mod"] = modSlot;

            var json = JsonSerializer.Serialize(pkg);
            var deserialized = JsonSerializer.Deserialize<SavePackage>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip });

            var restored = deserialized.ModData["test_mod"];
            Assert.NotNull(restored);
            Assert.Equal("test_mod", restored.Metadata.ModId);
            Assert.Equal("Test Mod", restored.Metadata.ModName);
            Assert.Equal("1.5.0", restored.Metadata.ModVersion);
            Assert.Equal("2.0.0", restored.Metadata.MinCoreVersion);
            Assert.False(restored.Metadata.IsRequired);
            Assert.Equal(42, restored.Data["custom"].GetInt("Code"));
            Assert.Equal("cthangover", restored.Data["custom"].GetString("Text"));
        }
        
        [Fact]
        public void Serialize_then_deserialize_restores_mod_slot_metadata_2()
        {
            var pkg = new SavePackage { SaveId = "slot_3", CoreVersion = "2.0.0" };
            var modSlot = new ModSlot
            {
                Metadata = new ModMetadata
                {
                    ModId = "test_mod",
                    ModName = "Test Mod",
                    ModVersion = "1.5.0",
                    MinCoreVersion = "2.0.0",
                    IsRequired = false
                }
            };
            
            modSlot.Data["custom"] = new DataBlob(1, 31);
            pkg.ModData["test_mod"] = modSlot;

            var json = JsonSerializer.Serialize(pkg);
            var deserialized = JsonSerializer.Deserialize<SavePackage>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip });

            var restored = deserialized.ModData["test_mod"];
            Assert.NotNull(restored);
            Assert.Equal("test_mod", restored.Metadata.ModId);
            Assert.Equal("Test Mod", restored.Metadata.ModName);
            Assert.Equal("1.5.0", restored.Metadata.ModVersion);
            Assert.Equal("2.0.0", restored.Metadata.MinCoreVersion);
            Assert.False(restored.Metadata.IsRequired);
            Assert.Equal(31, restored.Data["custom"].ReadJson<int>());
        }

        [Fact]
        public void Timestamp_survives_serialization()
        {
            var pkg = new SavePackage
            {
                SaveId = "slot_1",
                Timestamp = new System.DateTime(2025, 1, 15, 12, 30, 0, System.DateTimeKind.Utc),
                CoreVersion = "1.0.0"
            };

            var json = JsonSerializer.Serialize(pkg);
            var deserialized = JsonSerializer.Deserialize<SavePackage>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true, ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip });

            Assert.Equal(2025, deserialized.Timestamp.Year);
            Assert.Equal(1, deserialized.Timestamp.Month);
            Assert.Equal(15, deserialized.Timestamp.Day);
        }

        [Fact]
        public void ModData_is_initialized_on_construct()
        {
            var pkg = new SavePackage();
            Assert.NotNull(pkg.ModData);
            Assert.Empty(pkg.ModData);
        }
    }
}
