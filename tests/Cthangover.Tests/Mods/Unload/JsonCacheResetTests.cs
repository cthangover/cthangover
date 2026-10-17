namespace Cthangover.Tests.Mods.Unload
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Text.Json;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Unload;

    /// <summary>
    /// System.Text.Json caches type metadata and emitted accessors of every
    /// serialized type; for a mod type they pin the mod's context. After the
    /// reset the context of a serialized mod type can be collected.
    /// </summary>
    [Collection("mod-load-context")]
    public class JsonCacheResetTests
    {
        private const string ModSource = @"
namespace JsonMod
{
    public class Payload
    {
        public int Value { get; set; }
        public string Name { get; set; }
    }
}
";

        private static readonly JsonSerializerOptions SharedOptions = new();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference SerializeInCollectibleContext(string modId)
        {
            var compiled = TestMods.Compile(modId, ModSource);
            var dllPath = compiled.Location;
            ModAssemblyLoader.UnloadMod(modId);

            var context = new ModLoadContext(modId + "_json", null);
            context.StageAssembly(dllPath);
            var type = context.LoadStaged(modId).GetType("JsonMod.Payload");
            var payload = Activator.CreateInstance(type);
            type.GetProperty("Value").SetValue(payload, 42);
            var json = JsonSerializer.Serialize(payload, type, SharedOptions);
            Assert.Contains("42", json);
            Assert.Equal(42, type.GetProperty("Value").GetValue(JsonSerializer.Deserialize(json, type, SharedOptions)));

            var weak = new WeakReference(context);
            context.TryUnload();
            return weak;
        }

        [Fact]
        public void IsAvailable_OnThisRuntime()
        {
            Assert.True(JsonCacheReset.IsAvailable);
        }

        [Fact]
        public void Clear_ReleasesSerializedModTypes()
        {
            var weak = SerializeInCollectibleContext(TestMods.NewId("test_json"));

            Assert.True(JsonCacheReset.Clear());
            Assert.Null(ModLoadContext.CollectAndVerify(weak, "test_json"));
        }
    }
}
