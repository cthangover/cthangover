using System.Text.Json;
using Cthangover.Core.Utils;

namespace Engine.Tests.Utils
{
    public class PropertyDataTests
    {
        [Fact]
        public void ImportJsonProperty_Integer_SetsCorrectValue()
        {
            var props = new PropertyData();
            var json = JsonSerializer.Deserialize<JsonElement>("42");
            PropertyData.ImportJsonProperty(props, "Test", json);
            Assert.Equal(42, props.GetInt("Test"));
        }

        [Fact]
        public void ImportJsonProperty_Float_SetsCorrectValue()
        {
            var props = new PropertyData();
            var json = JsonSerializer.Deserialize<JsonElement>("3.14");
            PropertyData.ImportJsonProperty(props, "Test", json);
            Assert.Equal(3.14f, props.GetFloat("Test"));
        }

        [Fact]
        public void ImportJsonProperty_String_SetsCorrectValue()
        {
            var props = new PropertyData();
            var json = JsonSerializer.Deserialize<JsonElement>("\"hello\"");
            PropertyData.ImportJsonProperty(props, "Test", json);
            Assert.Equal("hello", props.GetStr("Test"));
        }

        [Fact]
        public void ImportJsonProperty_Bool_SetsCorrectValue()
        {
            var props = new PropertyData();
            var json = JsonSerializer.Deserialize<JsonElement>("true");
            PropertyData.ImportJsonProperty(props, "Test", json);
            Assert.True(props.GetBool("Test"));
        }

        [Fact]
        public void ImportJsonProperty_NonJsonElement_UsesToString()
        {
            var props = new PropertyData();
            PropertyData.ImportJsonProperty(props, "Test", 123);
            Assert.Equal("123", props.GetStr("Test"));
        }

        [Fact]
        public void ImportJsonProperty_Array_StoresAsRawText()
        {
            var props = new PropertyData();
            var json = JsonSerializer.Deserialize<JsonElement>("[1, 2]");
            PropertyData.ImportJsonProperty(props, "Test", json);
            Assert.Equal("[1, 2]", props.GetStr("Test"));
        }

        [Fact]
        public void ImportJsonProperties_Null_Dict_DoesNotThrow()
        {
            var props = new PropertyData();
            PropertyData.ImportJsonProperties(props, null);
            Assert.False(props.Has("anything"));
        }

        [Fact]
        public void Has_ExistingKey_ReturnsTrue()
        {
            var props = new PropertyData();
            props.SetInt("Test", 42);
            Assert.True(props.Has("Test"));
        }

        [Fact]
        public void Has_MissingKey_ReturnsFalse()
        {
            var props = new PropertyData();
            Assert.False(props.Has("Missing"));
        }

        [Fact]
        public void Clone_DeepCopiesValues()
        {
            var props = new PropertyData();
            props.SetStr("A", "1");
            props.SetStr("B", "2");

            var clone = props.Clone();

            Assert.Equal("1", clone.GetStr("A"));
            Assert.Equal("2", clone.GetStr("B"));

            clone.SetStr("A", "changed");
            Assert.Equal("1", props.GetStr("A"));
            Assert.Equal("changed", clone.GetStr("A"));
        }

        [Fact]
        public void GetInt_DefaultValue_OnMissing()
        {
            var props = new PropertyData();
            Assert.Equal(42, props.GetInt("missing", 42));
        }

        [Fact]
        public void GetStr_DefaultValue_OnMissing()
        {
            var props = new PropertyData();
            Assert.Equal("fallback", props.GetStr("missing", "fallback"));
        }

        [Fact]
        public void SetStr_OverwritesExisting()
        {
            var props = new PropertyData();
            props.SetStr("A", "first");
            props.SetStr("A", "second");
            Assert.Equal("second", props.GetStr("A"));
        }
    }
}
