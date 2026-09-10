using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;

namespace Engine.Tests.Mods.Mocks
{
    public class MockModSettings : IModSettings
    {
        public bool WriteValuesCalled { get; private set; }
        public bool ReadValuesCalled { get; private set; }
        public DataBlob LastWrittenBlob { get; private set; }
        public DataBlob LastReadBlob { get; private set; }

        private readonly List<ModSettingDefinition> _definitions;

        public MockModSettings(List<ModSettingDefinition> definitions = null)
        {
            _definitions = definitions ?? new List<ModSettingDefinition>();
        }

        public IReadOnlyList<ModSettingDefinition> GetDefinitions()
        {
            return _definitions;
        }

        public void WriteValues(DataBlob blob)
        {
            WriteValuesCalled = true;
            LastWrittenBlob = blob;
            blob.SetInt("test_int", 42);
            blob.SetString("test_str", "hello");
        }

        public void ReadValues(DataBlob blob)
        {
            ReadValuesCalled = true;
            LastReadBlob = blob;
        }
    }
}
