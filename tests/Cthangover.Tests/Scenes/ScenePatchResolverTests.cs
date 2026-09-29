using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes.Patching;
using Engine.Tests.Mods.Mocks;

namespace Engine.Tests.Scenes
{
    public class ScenePatchResolverTests
    {
        [Fact]
        public void CollectFromProviders_GroupsSameTargetAcrossModsInLoadOrder()
        {
            var modA = new MockModFileProvider("mod_a");
            modA.AddFile("patches/scenes/main_menu.tscn", PatchText("res://scenes/menu/main_menu.tscn", "A"));

            var modB = new MockModFileProvider("mod_b");
            modB.AddFile("patches/scenes/main_menu.tscn", PatchText("res://scenes/menu/main_menu.tscn", "B"));

            var result = ScenePatchResolver.CollectFromProviders(new[]
            {
                ("mod_a", (IModFileProvider)modA),
                ("mod_b", (IModFileProvider)modB),
            });

            Assert.True(result.TryGetValue("scenes/menu/main_menu.tscn", out var layers));
            Assert.Equal(2, layers.Count);
            Assert.Equal("mod_a", layers[0].ModId);
            Assert.Equal("mod_b", layers[1].ModId);
        }

        [Fact]
        public void CollectFromProviders_SortsFilesInsideOneMod()
        {
            var mod = new MockModFileProvider("mod_a");
            mod.AddFile("patches/scenes/20-second.tscn", PatchText("res://scenes/menu/main_menu.tscn", "second"));
            mod.AddFile("patches/scenes/00-first.tscn", PatchText("res://scenes/menu/main_menu.tscn", "first"));

            var result = ScenePatchResolver.CollectFromProviders(new[] { ("mod_a", (IModFileProvider)mod) });

            var layers = result["scenes/menu/main_menu.tscn"];
            Assert.EndsWith("00-first.tscn", layers[0].SourcePath);
            Assert.EndsWith("20-second.tscn", layers[1].SourcePath);
        }

        [Fact]
        public void CollectFromProviders_NestedDirectoriesAreScanned()
        {
            var mod = new MockModFileProvider("mod_a");
            mod.AddFile("patches/scenes/menu/main_menu.tscn", PatchText("res://scenes/menu/main_menu.tscn", "nested"));

            var result = ScenePatchResolver.CollectFromProviders(new[] { ("mod_a", (IModFileProvider)mod) });

            Assert.True(result.ContainsKey("scenes/menu/main_menu.tscn"));
        }

        [Fact]
        public void CollectFromProviders_NonPatchFilesAreSkipped()
        {
            var mod = new MockModFileProvider("mod_a");
            mod.AddFile("patches/scenes/broken.tscn", "[gd_scene format=3]\n\n[node name=\"Root\" type=\"Control\"]");
            mod.AddFile("patches/scenes/readme.txt", "not a scene");

            var result = ScenePatchResolver.CollectFromProviders(new[] { ("mod_a", (IModFileProvider)mod) });

            Assert.Empty(result);
        }

        [Fact]
        public void CollectFromProviders_ModTargetPrefixNormalized()
        {
            var mod = new MockModFileProvider("mod_a");
            mod.AddFile("patches/scenes/panel.tscn", PatchText("res://mods/mod_a/scenes/ui/panel.tscn", "x"));

            var result = ScenePatchResolver.CollectFromProviders(new[] { ("mod_a", (IModFileProvider)mod) });

            Assert.True(result.ContainsKey("scenes/ui/panel.tscn"));
        }

        private static string PatchText(string targetPath, string text)
        {
            return string.Join("\n",
                "[gd_scene format=3]",
                "",
                $"[ext_resource type=\"PackedScene\" path=\"{targetPath}\" id=\"1_target\"]",
                "",
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"Added\" type=\"Label\" parent=\".\"]",
                $"text = \"{text}\"");
        }
    }
}
