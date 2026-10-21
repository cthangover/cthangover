using System;
using System.IO;
using System.Text;
using Cthangover.Core.Mods.Caches;
using Engine.Tests.Mods.Mocks;

namespace Engine.Tests.Mods
{
    /// <summary>
    /// The on-disk shader cache of zip mods (Godot loads shaders only from a
    /// file path): an extracted copy belongs to the mod that shipped it and
    /// follows its content, so an updated mod is not served its old shader
    /// and a same-named shader of another mod never overwrites it.
    /// </summary>
    public class ModCacheManagerTests : IDisposable
    {
        private readonly string modA = "__test_shader_a_" + Guid.NewGuid().ToString("N");
        private readonly string modB = "__test_shader_b_" + Guid.NewGuid().ToString("N");

        public void Dispose()
        {
            foreach (var mod in new[] { modA, modB })
            {
                var dir = Path.GetDirectoryName(ModCacheManager.GetShaderCacheDir(mod, "glow"));
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
        }

        [Fact]
        public void ExtractShaders_FollowsContent_AndKeepsModsApart()
        {
            ModCacheManager.ExtractShaders(modA, Shaders(modA, "shader_type canvas_item; // a1"));
            ModCacheManager.ExtractShaders(modA, Shaders(modA, "shader_type canvas_item; // a2"));
            ModCacheManager.ExtractShaders(modB, Shaders(modB, "shader_type canvas_item; // b"));

            Assert.True(ModCacheManager.IsShaderCached(modA, "glow"));
            Assert.Equal("shader_type canvas_item; // a2", File.ReadAllText(ModCacheManager.GetShaderCachePath(modA, "glow")));
            Assert.Equal("shader_type canvas_item; // b", File.ReadAllText(ModCacheManager.GetShaderCachePath(modB, "glow")));
        }

        private static MockModFileProvider Shaders(string mod, string glow)
        {
            var provider = new MockModFileProvider(mod);
            provider.AddBinaryFile("shaders/glow.gdshader", Encoding.UTF8.GetBytes(glow));
            return provider;
        }
    }
}
