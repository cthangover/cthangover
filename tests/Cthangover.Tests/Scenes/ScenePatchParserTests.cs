using Cthangover.Core.Scenes.Patching;

namespace Engine.Tests.Scenes
{
    public class ScenePatchParserTests
    {
        private const string ValidPatch = @"
[gd_scene format=3]

[ext_resource type=""PackedScene"" path=""res://mods/my_mod/scenes/menu/main_menu.tscn"" id=""1_target""]

[node name=""MainMenuScene"" instance=ExtResource(""1_target"")]
";

        [Fact]
        public void TryParse_ValidInheritedScene_ResolvesTarget()
        {
            var ok = ScenePatchParser.TryParse(ValidPatch, "my_mod", "patches/scenes/main_menu.tscn", out var patch, out var error);

            Assert.True(ok, error);
            Assert.Equal("scenes/menu/main_menu.tscn", patch.TargetPath);
            Assert.Equal("my_mod", patch.ModId);
            Assert.Equal("patches/scenes/main_menu.tscn", patch.SourcePath);
            Assert.Single(patch.Scene.Nodes);
            Assert.Equal("MainMenuScene", patch.Scene.Nodes[0].Name);
        }

        [Fact]
        public void TryParse_NoInstance_Rejected()
        {
            const string text = @"
[gd_scene format=3]

[node name=""Root"" type=""Control""]
";
            Assert.False(ScenePatchParser.TryParse(text, "m", "p.tscn", out _, out var error));
            Assert.Contains("instance", error);
        }

        [Fact]
        public void TryParse_UnknownExtResource_Rejected()
        {
            const string text = @"
[gd_scene format=3]

[node name=""Root"" instance=ExtResource(""missing"")]
";
            Assert.False(ScenePatchParser.TryParse(text, "m", "p.tscn", out _, out var error));
            Assert.Contains("unknown ext_resource", error);
        }

        [Fact]
        public void TryParse_NonPackedSceneTarget_Rejected()
        {
            const string text = @"
[gd_scene format=3]

[ext_resource type=""Script"" path=""src/Foo.cs"" id=""1_s""]

[node name=""Root"" instance=ExtResource(""1_s"")]
";
            Assert.False(ScenePatchParser.TryParse(text, "m", "p.tscn", out _, out var error));
            Assert.Contains("PackedScene", error);
        }

        [Fact]
        public void TryParse_NoRoot_Rejected()
        {
            const string text = @"
[gd_scene format=3]

[node name=""Child"" type=""Label"" parent="".""]
";
            Assert.False(ScenePatchParser.TryParse(text, "m", "p.tscn", out _, out _));
        }

        [Fact]
        public void TryParse_Empty_Rejected()
        {
            Assert.False(ScenePatchParser.TryParse("", "m", "p.tscn", out _, out _));
        }
    }
}
