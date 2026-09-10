using System.Reflection;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;

namespace Engine.Tests.Mods
{
    public class ModSaveableRegistryTests
    {
        private static readonly System.Type RegisteredType =
            typeof(ModSaveableRegistry).GetNestedType("RegisteredSaveable",
                BindingFlags.NonPublic | BindingFlags.Static);

        private static readonly FieldInfo SaveablesField =
            typeof(ModSaveableRegistry).GetField("_saveables",
                BindingFlags.NonPublic | BindingFlags.Static);

        private static void SetSaveables(params (IModSaveable instance, string modId)[] entries)
        {
            var list = (System.Collections.IList)SaveablesField.GetValue(null);
            list.Clear();
            foreach (var (instance, modId) in entries)
            {
                var reg = System.Activator.CreateInstance(RegisteredType);
                RegisteredType.GetField("Instance").SetValue(reg, instance);
                RegisteredType.GetField("ModId").SetValue(reg, modId);
                list.Add(reg);
            }
        }

        private static readonly FieldInfo RegistryInitializedField =
            typeof(ModRegistry).GetField("initialized",
                BindingFlags.NonPublic | BindingFlags.Instance);

        private static void InitModRegistry()
        {
            var registry = ModRegistry.Instance;
            if (!(bool)RegistryInitializedField.GetValue(registry))
            {
                RegistryInitializedField.SetValue(registry, true);
            }
        }

        // ─── CollectSaveData ───────────────────────────────────────

        [Fact]
        public void CollectSaveData_OnSave_is_called_and_slot_metadata_is_prefilled()
        {
            InitModRegistry();
            var saveable = new Mocks.MockModSaveable();
            SetSaveables((saveable, "core"));

            var result = ModSaveableRegistry.CollectSaveData();

            Assert.True(saveable.OnSaveCalled);
            var slot = saveable.SavedSlot;
            Assert.NotNull(slot);
            Assert.NotNull(slot.Data);

            Assert.Equal("core", slot.Metadata.ModId);
            Assert.Equal("core", slot.Metadata.ModName);
            Assert.Equal("0.0.0", slot.Metadata.ModVersion);
        }

        [Fact]
        public void CollectSaveData_returns_slot_keyed_by_modId()
        {
            InitModRegistry();
            var saveableA = new Mocks.MockModSaveable();
            var saveableB = new Mocks.MockModSaveable();
            SetSaveables((saveableA, "mod_a"), (saveableB, "mod_b"));

            var result = ModSaveableRegistry.CollectSaveData();

            Assert.True(result.ContainsKey("mod_a"));
            Assert.True(result.ContainsKey("mod_b"));
            Assert.Same(saveableA.SavedSlot, result["mod_a"]);
            Assert.Same(saveableB.SavedSlot, result["mod_b"]);
        }

        [Fact]
        public void CollectSaveData_preserves_data_written_by_saveable()
        {
            InitModRegistry();
            var saveable = new Mocks.MockModSaveable();
            saveable.OnSaveData = s =>
            {
                s.Data["stats"] = new DataBlob(1, 42);
                s.Data["level"] = new DataBlob(1, 7);
            };
            SetSaveables((saveable, "core"));

            var result = ModSaveableRegistry.CollectSaveData();

            var slot = result["core"];
            Assert.True(slot.Data.ContainsKey("stats"));
            Assert.True(slot.Data.ContainsKey("level"));
            Assert.Equal(42, slot.Data["stats"].ReadJson<int>());
            Assert.Equal(7, slot.Data["level"].ReadJson<int>());
        }

        [Fact]
        public void CollectSaveData_exception_in_OnSave_is_caught_and_slot_is_not_added()
        {
            InitModRegistry();
            var saveable = new Mocks.MockModSaveable { ShouldThrowOnSave = true };
            SetSaveables((saveable, "core"));

            var result = ModSaveableRegistry.CollectSaveData();

            Assert.False(saveable.OnSaveCalled);
            Assert.False(result.ContainsKey("core"));
        }

        // ─── DispatchLoad ─────────────────────────────────────────

        [Fact]
        public void DispatchLoad_finds_slot_by_modId_and_calls_OnLoad()
        {
            var saveable = new Mocks.MockModSaveable { UseOverload = true };
            SetSaveables((saveable, "my_mod"));

            var slot = new ModSlot
            {
                Metadata = new ModMetadata { ModId = "my_mod", ModVersion = "1.0" },
            };
            slot.Data["payload"] = new DataBlob(1, 99);

            var errors = ModSaveableRegistry.DispatchLoad(
                new System.Collections.Generic.Dictionary<string, ModSlot> { ["my_mod"] = slot });

            Assert.Empty(errors);
            Assert.True(saveable.OnLoadCalled);
            Assert.Same(slot, saveable.LoadedSlot);
            Assert.Equal(99, saveable.LoadedSlot.Data["payload"].ReadJson<int>());
        }

        [Fact]
        public void DispatchLoad_when_OnLoad_returns_false_adds_error_message()
        {
            var saveable = new Mocks.MockModSaveable
            {
                UseOverload = true,
                OnLoadResult = false,
                OnLoadError = "data corrupted"
            };
            SetSaveables((saveable, "broken_mod"));

            var errors = ModSaveableRegistry.DispatchLoad(
                new System.Collections.Generic.Dictionary<string, ModSlot> { ["broken_mod"] = new ModSlot() });

            Assert.Single(errors);
            Assert.Contains("data corrupted", errors[0]);
        }

        [Fact]
        public void DispatchLoad_when_OnLoad_throws_adds_error_message()
        {
            var saveable = new Mocks.MockModSaveable
            {
                UseOverload = true,
                ShouldThrowOnLoad = true
            };
            SetSaveables((saveable, "throw_mod"));

            var errors = ModSaveableRegistry.DispatchLoad(
                new System.Collections.Generic.Dictionary<string, ModSlot> { ["throw_mod"] = new ModSlot() });

            Assert.Single(errors);
            Assert.Contains("Test load error", errors[0]);
        }

        [Fact]
        public void DispatchLoad_default_OnLoad_delegates_to_OnLoadVoid()
        {
            var saveable = new Mocks.MockModSaveable();
            SetSaveables((saveable, "void_mod"));

            var errors = ModSaveableRegistry.DispatchLoad(
                new System.Collections.Generic.Dictionary<string, ModSlot> { ["void_mod"] = new ModSlot() });

            Assert.Empty(errors);
            Assert.True(saveable.OnLoadCalled);
        }

        [Fact]
        public void DispatchLoad_not_saved_not_called()
        {
            var saveable = new Mocks.MockModSaveable { UseOverload = true };
            SetSaveables((saveable, "unsaved_mod"));

            var errors = ModSaveableRegistry.DispatchLoad(
                new System.Collections.Generic.Dictionary<string, ModSlot>());

            Assert.Empty(errors);
            Assert.False(saveable.OnLoadCalled);
        }

        [Fact]
        public void DispatchLoad_null_modData_returns_empty()
        {
            var saveable = new Mocks.MockModSaveable();
            SetSaveables((saveable, "test_mod"));

            var errors = ModSaveableRegistry.DispatchLoad(null);

            Assert.Empty(errors);
        }
    }
}
