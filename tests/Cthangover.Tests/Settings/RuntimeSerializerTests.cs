using System.Text.Json;
using Cthangover.Core.Actions;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Tool;

namespace Engine.Tests.Settings
{
    [Collection("ConditionStateMonitor")]
    public class RuntimeSerializerTests
    {
        private static RuntimeData CreateRuntime()
        {
            var runtime = new RuntimeData();
            return runtime;
        }

        [Fact]
        public void BuildCoreSlot_HasAllBlobs()
        {
            var runtime = CreateRuntime();
            var slot = RuntimeSerializer.BuildCoreSlot(runtime, "1.0.0", "test_scene", new HashSet<string>());

            Assert.NotNull(slot);
            Assert.NotNull(slot.Data);
            Assert.True(slot.Data.ContainsKey("Time"));
            Assert.True(slot.Data.ContainsKey("Lamp"));
            Assert.True(slot.Data.ContainsKey("ActionPool"));
            Assert.True(slot.Data.ContainsKey("Skills"));
            Assert.True(slot.Data.ContainsKey("CompletedScenarios"));
            Assert.True(slot.Data.ContainsKey("MapZones"));

            // Roster persistence belongs to the characters_base engine mod's
            // IModSaveable slot (task-11), not the core slot.
            Assert.False(slot.Data.ContainsKey("Characters"));
            Assert.False(slot.Data.ContainsKey("BattleSet"));
        }

        [Fact]
        public void BuildCoreSlot_MetadataIsRequiredTrue()
        {
            var runtime = CreateRuntime();
            var slot = RuntimeSerializer.BuildCoreSlot(runtime, "1.0.0", "test_scene", new HashSet<string>());

            Assert.True(slot.Metadata.IsRequired);
            Assert.Equal(SavePackage.CoreSlotKey, slot.Metadata.ModId);
            Assert.Equal("Core Game", slot.Metadata.ModName);
            Assert.Equal("1.0.0", slot.Metadata.MinCoreVersion);
        }

        [Fact]
        public void BuildCoreSlot_PreviewMetadata()
        {
            var runtime = CreateRuntime();
            runtime.Time.SetTime(1000);
            runtime.CharacterData.Characters = new Dictionary<string, CharacterInfoData>
            {
                ["hero"] = new CharacterInfoData { ID = "hero", CharacterType = "hero" },
                ["mage"] = new CharacterInfoData { ID = "mage", CharacterType = "mage" }
            };

            var slot = RuntimeSerializer.BuildCoreSlot(runtime, "1.0.0", "my_scene", new HashSet<string>());

            Assert.NotNull(slot.Metadata.CustomData);

            var preview = JsonSerializer.Deserialize<RuntimeSerializer.CoreSlotPreview>(slot.Metadata.CustomData);
            Assert.NotNull(preview);
            Assert.Equal("my_scene", preview.CurrentSceneName);
            Assert.Equal(1000L, preview.GameTime);
            Assert.Equal(2, preview.CharacterCount);
        }

        [Fact]
        public void BuildCoreSlot_TimeBlob()
        {
            var runtime = CreateRuntime();
            runtime.Time.SetTime(12345);

            var slot = RuntimeSerializer.BuildCoreSlot(runtime, "1.0.0", "test", new HashSet<string>());

            Assert.True(slot.Data.ContainsKey("Time"));
            var restored = slot.Data["Time"].ReadJson<long>();
            Assert.Equal(12345L, restored);
        }

        [Fact]
        public void BuildCoreSlot_LampBlob()
        {
            var runtime = CreateRuntime();
            runtime.LampData.Radius = 150f;
            runtime.LampData.Influence = 0.75f;

            var slot = RuntimeSerializer.BuildCoreSlot(runtime, "1.0.0", "test", new HashSet<string>());

            var lampBlob = slot.Data["Lamp"];
            Assert.Equal(150f, lampBlob.GetFloat("Radius", 0f));
            Assert.Equal(0.75f, lampBlob.GetFloat("Influence", 0f));
        }

        [Fact]
        public void BuildCoreSlot_CompletedScenariosBlob()
        {
            var runtime = CreateRuntime();
            var completed = new HashSet<string> { "scene_1", "scene_2" };

            var slot = RuntimeSerializer.BuildCoreSlot(runtime, "1.0.0", "test", completed);

            var blob = slot.Data["CompletedScenarios"];
            var list = blob.ReadJson<List<string>>();
            Assert.Contains("scene_1", list);
            Assert.Contains("scene_2", list);
        }

        [Fact]
        public void BuildCoreSlot_ThenRestoreCoreState_Roundtrip()
        {
            var runtime = CreateRuntime();
            runtime.Time.SetTime(50000);
            runtime.LampData.Radius = 200f;
            runtime.LampData.Influence = 0.5f;

            var slot = RuntimeSerializer.BuildCoreSlot(runtime, "1.0.0", "test", new HashSet<string>());

            var restored = CreateRuntime();
            RuntimeSerializer.RestoreCoreState(slot, restored);

            Assert.Equal(50000L, restored.Time.Tick);
            Assert.Equal(200f, restored.LampData.Radius);
            Assert.Equal(0.5f, restored.LampData.Influence);
        }

        [Fact]
        public void BuildCoreSlot_ThenRestoreCoreState_RestoresStoryFlags()
        {
            var runtime = CreateRuntime();
            runtime.Flags["door"] = "open";
            runtime.Flags["visits"] = "3";

            var json = JsonSerializer.Serialize(RuntimeSerializer.BuildCoreSlot(runtime, "1.0.0", "test", new HashSet<string>()));
            var restored = CreateRuntime();
            RuntimeSerializer.RestoreCoreState(JsonSerializer.Deserialize<ModSlot>(json), restored);

            Assert.Equal(new Dictionary<string, string> { ["door"] = "open", ["visits"] = "3" }, restored.Flags);
        }

        [Fact]
        public void BuildCoreSlot_ThenRestoreCoreState_RestoresSkills()
        {
            var runtime = CreateRuntime();
            runtime.SkillData.AddSkill("slave_master");
            runtime.SkillData.AddSkill("battle_instinct");

            var slot = RuntimeSerializer.BuildCoreSlot(runtime, "1.0.0", "test", new HashSet<string>());

            var restored = CreateRuntime();
            restored.SkillData.AddSkill("stale_from_previous_game");
            RuntimeSerializer.RestoreCoreState(slot, restored);

            Assert.True(restored.SkillData.HasSkill("slave_master"));
            Assert.True(restored.SkillData.HasSkill("battle_instinct"));
            Assert.False(restored.SkillData.HasSkill("stale_from_previous_game"));
        }

        [Fact]
        public void RestoreCoreState_NullData_NoCrash()
        {
            var slot = new ModSlot { Data = null };
            var runtime = CreateRuntime();
            var originalTick = runtime.Time.Tick;

            RuntimeSerializer.RestoreCoreState(slot, runtime);

            Assert.Equal(originalTick, runtime.Time.Tick);
        }

        [Fact]
        public void RestoreCoreState_CompletedScenarios_ReplacesHistory()
        {
            var runtime = CreateRuntime();
            runtime.CompletedScenarioIds.Add("stale_from_previous_game");
            runtime.CompletedScenarioIds.Add("morning_wake");

            var source = CreateRuntime();
            var slot = RuntimeSerializer.BuildCoreSlot(source, "1.0.0", "test", new HashSet<string> { "prologue_done" });

            RuntimeSerializer.RestoreCoreState(slot, runtime);

            Assert.DoesNotContain("stale_from_previous_game", runtime.CompletedScenarioIds);
            Assert.DoesNotContain("morning_wake", runtime.CompletedScenarioIds);
            Assert.Contains("prologue_done", runtime.CompletedScenarioIds);
            Assert.NotNull(runtime.LoadedCompletedScenarioIds);
            Assert.Contains("prologue_done", runtime.LoadedCompletedScenarioIds);
        }

        [Fact]
        public void RestoreCoreState_EmptyCompletedList_ClearsHistory()
        {
            var runtime = CreateRuntime();
            runtime.CompletedScenarioIds.Add("leftover");

            var source = CreateRuntime();
            var slot = RuntimeSerializer.BuildCoreSlot(source, "1.0.0", "test", new HashSet<string>());

            RuntimeSerializer.RestoreCoreState(slot, runtime);

            Assert.Empty(runtime.CompletedScenarioIds);
        }

        [Fact]
        public void RestoreCoreState_EmptyData_NoCrash()
        {
            var slot = new ModSlot { Data = new Dictionary<string, DataBlob>() };
            var runtime = CreateRuntime();
            var originalTick = runtime.Time.Tick;

            RuntimeSerializer.RestoreCoreState(slot, runtime);

            Assert.Equal(originalTick, runtime.Time.Tick);
        }

        [Fact]
        public void MapZonesBlob_ThenRestoreCoreState_Roundtrip()
        {
            // Zone overrides live in the map_base engine mod now, so the
            // kernel round-trip is exercised through MapStateBridge with a
            // stand-in backend (registration is first-wins for the assembly).
            var live = new MapZoneStateData();
            MapStateBridge.Register(
                () => live,
                value => live = value,
                () => live = new MapZoneStateData());

            live.Open["locked_zone"] = true;
            live.Visible["hidden_zone"] = false;

            var slot = RuntimeSerializer.BuildCoreSlot(CreateRuntime(), "1.0.0", "test", new HashSet<string>());

            live = new MapZoneStateData();
            Assert.Empty(live.Open);

            var restored = CreateRuntime();
            RuntimeSerializer.RestoreCoreState(slot, restored);

            Assert.True(live.Open.TryGetValue("locked_zone", out var open) && open);
            Assert.True(live.Visible.TryGetValue("hidden_zone", out var visible) && !visible);
        }

        [Fact]
        public void ReadSlotPreview_NullMetadata_ReturnsDefaults()
        {
            var result = RuntimeSerializer.ReadSlotPreview(null);

            Assert.Null(result.CurrentSceneName);
            Assert.Equal(0L, result.GameTime);
            Assert.Equal(0, result.CharacterCount);
        }

        [Fact]
        public void ReadSlotPreview_InvalidJson_ReturnsDefaults()
        {
            var meta = new ModMetadata { CustomData = "{broken" };
            var result = RuntimeSerializer.ReadSlotPreview(meta);

            Assert.Null(result.CurrentSceneName);
        }

        [Fact]
        public void ReadSlotPreview_ValidJson()
        {
            var preview = new RuntimeSerializer.CoreSlotPreview
            {
                CurrentSceneName = "BattleScene",
                GameTime = 9999,
                CharacterCount = 3
            };
            var meta = new ModMetadata
            {
                CustomData = JsonSerializer.Serialize(preview)
            };

            var result = RuntimeSerializer.ReadSlotPreview(meta);

            Assert.Equal("BattleScene", result.CurrentSceneName);
            Assert.Equal(9999L, result.GameTime);
            Assert.Equal(3, result.CharacterCount);
        }
    }
}
