#if TOOLS
using Cthangover.Core.Autotest;
using Cthangover.Core.Scenes.TscnBuild;
using Cthangover.Core.Scenes.TscnParsing;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    public partial class TscnBuildTestSetup : Node
    {
        private int _passed;
        private int _failed;

        public override void _Ready()
        {
            GameLogger.Log("TSCN_TEST", "TscnBuildTestSetup: starting integration test");

            TscnTestEntryPoint.Reset();

            var tscn = @"
[gd_scene format=3]

[node name=""Root"" type=""Control"" groups=[""tscn_test_group""] __entry_point=""Cthangover.Core.Autotest.TscnTestEntryPoint""]
anchor_right = 1.0
anchor_bottom = 1.0

[node name=""MyPanel"" parent=""."" type=""Panel""]
layout_mode = 1

[node name=""MyButton"" parent=""MyPanel"" type=""Button""]
text = ""Click Me""
disabled = false

[node name=""MyLabel"" parent=""MyPanel"" type=""Label""]
text = ""Hello World""

[connection signal=""pressed"" from=""MyButton"" to=""."" method=""OnPressed""]
".Trim();

            var parser = new TscnParser();
            var scene = parser.Parse(tscn);

            Assert(scene.Nodes.Count == 4, "Parse: 4 nodes");
            Assert(scene.Connections.Count == 0, "Parse: 0 connections (OnPressed not on root Control)");
            Assert(scene.Nodes[0].Groups != null && scene.Nodes[0].Groups.Length == 1, "Parse: root has 1 group");
            Assert(scene.Nodes[0].Groups[0] == "tscn_test_group", "Parse: group name correct");
            Assert(scene.EntryPointClass == "Cthangover.Core.Autotest.TscnTestEntryPoint", "Parse: entry point class stored");

            var builder = new SceneBuilder();
            var root = builder.Build(scene, null, null);

            Assert(root != null, "Build: root not null");
            Assert(root.Name == "Root", "Build: root name is Root");
            Assert(root.IsInGroup("tscn_test_group"), "Build: root in tscn_test_group");

            var panel = root.GetNodeOrNull<Panel>("MyPanel");
            Assert(panel != null, "Build: MyPanel exists");
            Assert(panel.GetParent() == root, "Build: MyPanel parent is Root");

            var button = root.GetNodeOrNull<Button>("MyPanel/MyButton");
            Assert(button != null, "Build: MyButton exists");
            Assert(button.Text == "Click Me", "Build: button text correct");
            Assert(button.Disabled == false, "Build: button not disabled");

            var label = root.GetNodeOrNull<Label>("MyPanel/MyLabel");
            Assert(label != null, "Build: MyLabel exists");
            Assert(label.Text == "Hello World", "Build: label text correct");

            var signalFired = false;
            button.Connect("pressed", Callable.From(() => { signalFired = true; }));
            button.EmitSignal("pressed");
            Assert(signalFired, "Build: signal pressed fired and handled");

            Assert(TscnTestEntryPoint.WasCalled, "Build: entry point called");

            // --- ExtResource texture test ---
            var tscnTex = @"
[gd_scene format=3]

[ext_resource type=""Texture2D"" path=""res://test_tex.png"" id=""1_tex""]

[node name=""TexRoot"" type=""Control""]
[node name=""TexChild"" parent=""."" type=""TextureRect""]
texture = ExtResource(""1_tex"")
".Trim();

            var parsedTex = parser.Parse(tscnTex);
            Assert(parsedTex.ExtResources.Count == 1, "ExtRes: 1 ext_resource parsed");
            Assert(parsedTex.ExtResources[0].Type == "Texture2D", "ExtRes: type is Texture2D");
            Assert(parsedTex.ExtResources[0].Path == "res://test_tex.png", "ExtRes: path correct");

            var texResolved = false;
            Texture2D resolvedTex = null;
            var texRoot = builder.Build(parsedTex, null, refId =>
            {
                texResolved = true;
                var img = Image.Create(2, 2, false, Image.Format.Rgba8);
                resolvedTex = ImageTexture.CreateFromImage(img);
                return resolvedTex;
            });

            Assert(texRoot != null, "ExtRes: root not null");
            Assert(texResolved, "ExtRes: resolver callback was invoked");

            var texRect = texRoot.GetNodeOrNull<TextureRect>("TexChild");
            Assert(texRect != null, "ExtRes: TexChild exists");
            Assert(texRect.Texture != null, "ExtRes: texture assigned");
            Assert(texRect.Texture == resolvedTex, "ExtRes: texture is the one we returned");

            GameLogger.Log("TSCN_TEST", $"TscnBuildTestSetup: {_passed} passed, {_failed} failed");
            if (_failed == 0)
                GameLogger.Log("TSCN_TEST", "All integration tests PASSED");

            GetTree().Quit();
        }

        private void Assert(bool condition, string msg)
        {
            if (condition)
            {
                _passed++;
                GameLogger.Log("TSCN_TEST", $"  [PASS] {msg}");
            }
            else
            {
                _failed++;
                GameLogger.Log("TSCN_TEST", $"  [FAIL] {msg}", LogLevel.Error);
            }
        }
    }
}
#endif
