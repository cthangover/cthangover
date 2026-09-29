#if TOOLS
using System.IO;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.Scenes.Patching;
using Cthangover.Core.Scenes.TscnParsing;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
    /// <summary>
    /// Integration coverage for scene patches: creates a temporary mod on disk (so the test is
    /// independent of the user's disabled-mod settings), reloads the mod registry, then loads the
    /// patched scene through <see cref="TscnScenes"/> and asserts the three operations Godot's
    /// inheritance cannot express — a masked node delete, a property removal and a node addition
    /// — plus a plain property override. The temporary mod is removed and the registry reloaded
    /// in a finally block.
    /// </summary>
    public partial class ScenePatchTestSetup : Node
    {
        private const string TargetScene = "scenes/patch_target.tscn";
        private const string TestModId = "scene_patch_test_mod";

        private const string TargetSceneText = @"[gd_scene format=3]

[node name=""PatchTarget"" type=""Control""]

[node name=""Title"" type=""Label"" parent="".""]
text = ""Original""

[node name=""OldNode"" type=""Node"" parent="".""]

[node name=""Panel"" type=""Panel"" parent="".""]
tooltip_text = ""remove-me""
";

        private const string PatchSceneText = @"[gd_scene format=3]

[ext_resource type=""PackedScene"" path=""res://mods/scene_patch_test_mod/scenes/patch_target.tscn"" id=""1_target""]

[node name=""PatchTarget"" instance=ExtResource(""1_target"")]
metadata/_patch_delete_nodes = PackedStringArray(""OldNode"")

[node name=""Title"" parent="".""]
text = ""Patched""

[node name=""AddedLabel"" type=""Label"" parent="".""]
text = ""Added""

[node name=""Panel"" parent="".""]
metadata/_patch_add_groups = PackedStringArray(""patched_group"")
metadata/_patch_remove = ""tooltip_text""
";

        private int _passed;
        private int _failed;

        public override void _Ready()
        {
            GameLogger.Log("TEST", "ScenePatchTestSetup: starting scene patch integration test");

            if (!ModManager.Instance.IsInitialized)
                ModManager.Instance.Initialize();

            TestResolverPatching();
            TestHasPatches();
            TestApplierOnSyntheticScene();

            GameLogger.Log("TEST", $"ScenePatchTestSetup: {_passed} passed, {_failed} failed");
            GameLogger.Log("TEST", $"[TEST_RESULT] name=scene_patch passed={_passed} failed={_failed}");
            GetTree().Quit(_failed == 0 ? 0 : 1);
        }

        private void TestResolverPatching()
        {
            var modDirectory = Path.Combine(ModRegistry.ModsPath, TestModId).Replace('\\', '/');

            try
            {
                CreateMod(modDirectory);
                ModManager.Instance.Reload();

                var root = TscnScenes.LoadAndBuild(TargetScene);
                Assert(root != null, "resolver: patched scene loads");
                if (root == null)
                    return;

                Assert(root.Name == "PatchTarget", "resolver: root name preserved");

                var title = root.GetNodeOrNull<Label>("Title");
                Assert(title != null, "resolver: Title exists");
                Assert(title != null && title.Text == "Patched", "resolver: overridden property applied");

                Assert(root.GetNodeOrNull("OldNode") == null, "resolver: masked node deleted");

                var added = root.GetNodeOrNull<Label>("AddedLabel");
                Assert(added != null, "resolver: added node exists");
                Assert(added != null && added.Text == "Added", "resolver: added node property");

                var panel = root.GetNodeOrNull<Panel>("Panel");
                Assert(panel != null && panel.IsInGroup("patched_group"), "resolver: group operation applied");
                Assert(panel != null && string.IsNullOrEmpty(panel.TooltipText), "resolver: removed property stays default");
            }
            finally
            {
                try
                {
                    if (Directory.Exists(modDirectory))
                        Directory.Delete(modDirectory, true);
                }
                catch (System.Exception ex)
                {
                    GameLogger.Log("TEST", $"ScenePatchTestSetup: failed to remove temporary mod: {ex.Message}", LogLevel.Warning);
                }

                ModManager.Instance.Reload();
            }
        }

        private void TestHasPatches()
        {
            Assert(!TscnScenes.HasPatches("scenes/does_not_exist.tscn"), "resolver: HasPatches false for unknown scene");
        }

        private void TestApplierOnSyntheticScene()
        {
            var target = new TscnParser().Parse(string.Join("\n",
                "[gd_scene format=3]",
                "[node name=\"Root\" type=\"Control\"]",
                "[node name=\"Child\" type=\"Label\" parent=\".\"]",
                "text = \"before\""));

            var patchText = string.Join("\n",
                "[gd_scene format=3]",
                "[ext_resource type=\"PackedScene\" path=\"res://scenes/synthetic.tscn\" id=\"1_target\"]",
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"Child\" parent=\".\"]",
                "text = \"after\"",
                "[node name=\"Added\" type=\"Label\" parent=\".\"]");

            var ok = ScenePatchParser.TryParse(patchText, "test", "patches/scenes/synthetic.tscn", out var patch, out var error);
            Assert(ok, $"applier: patch parses ({error})");
            if (!ok)
                return;

            var applied = new ScenePatchApplier().ApplyAll(target, new[] { patch });
            Assert(applied, "applier: ApplyAll succeeds");
            Assert(target.Nodes.Count == 3, "applier: synthetic scene has 3 nodes");

            var debug = ScenePatchDebug.Format(target);
            Assert(debug.Contains("Added"), "debug: dump contains added node");
        }

        private static void CreateMod(string modDirectory)
        {
            Directory.CreateDirectory(modDirectory + "/scenes");
            Directory.CreateDirectory(modDirectory + "/patches/scenes");

            File.WriteAllText(modDirectory + "/manifest.json",
                "{\n" +
                $"  \"id\": \"{TestModId}\",\n" +
                "  \"name\": \"Scene Patch Test\",\n" +
                "  \"version\": \"1.0.0\",\n" +
                "  \"author\": \"ct\",\n" +
                "  \"description\": \"Temporary mod created by the scene patch integration test\",\n" +
                "  \"category\": \"other\"\n" +
                "}\n");

            File.WriteAllText(modDirectory + "/scenes/patch_target.tscn", TargetSceneText);
            File.WriteAllText(modDirectory + "/patches/scenes/patch_target.tscn", PatchSceneText);
        }

        private void Assert(bool condition, string message)
        {
            if (condition)
            {
                _passed++;
                GameLogger.Log("TEST", $"  [PASS] {message}");
            }
            else
            {
                _failed++;
                GameLogger.Log("TEST", $"  [FAIL] {message}", LogLevel.Error);
            }
        }
    }
}
#endif
