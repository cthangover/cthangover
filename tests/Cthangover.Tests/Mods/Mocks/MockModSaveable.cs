using System;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;

namespace Engine.Tests.Mods.Mocks
{
    public class MockModSaveable : IModSaveable
    {
        public bool OnSaveCalled { get; private set; }
        public ModSlot SavedSlot { get; private set; }
        public bool ShouldThrowOnSave { get; set; }
        public Action<ModSlot> OnSaveData { get; set; }

        public bool OnLoadCalled { get; private set; }
        public ModSlot LoadedSlot { get; private set; }
        public bool OnLoadResult { get; set; } = true;
        public string OnLoadError { get; set; }
        public bool ShouldThrowOnLoad { get; set; }

        public bool UseOverload { get; set; }

        public void OnSave(ModSlot slot)
        {
            if (ShouldThrowOnSave)
                throw new Exception("Test save error");
            OnSaveCalled = true;
            SavedSlot = slot;
            OnSaveData?.Invoke(slot);
        }

        public bool OnLoad(ModSlot slot, out string errorMessage)
        {
            if (UseOverload)
            {
                if (ShouldThrowOnLoad)
                    throw new Exception("Test load error");
                OnLoadCalled = true;
                LoadedSlot = slot;
                errorMessage = OnLoadError;
                return OnLoadResult;
            }

            OnLoadVoid(slot);
            errorMessage = null;
            return true;
        }

        public void OnLoadVoid(ModSlot slot)
        {
            if (ShouldThrowOnLoad)
                throw new Exception("Test load error");
            OnLoadCalled = true;
            LoadedSlot = slot;
        }
    }
}
