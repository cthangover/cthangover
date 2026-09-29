using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Scenes.Patching;
using Cthangover.Core.Scenes.TscnParsing;

namespace Engine.Tests.Scenes
{
    public class ScenePatchApplierTests
    {
        private static readonly string[] TargetLines =
        {
            "[gd_scene format=3]",
            "",
            "[ext_resource type=\"Script\" path=\"src/Root.cs\" id=\"1_script\"]",
            "[ext_resource type=\"Texture2D\" path=\"res://tex/original.png\" id=\"1_tex\"]",
            "[ext_resource type=\"Texture2D\" path=\"res://tex/keep.png\" id=\"2_tex\"]",
            "[ext_resource type=\"Texture2D\" path=\"res://tex/orphan.png\" id=\"4_tex\"]",
            "",
            "[sub_resource type=\"StyleBoxFlat\" id=\"sb1\"]",
            "bg_color = Color(0, 0, 0, 1)",
            "",
            "[sub_resource type=\"StyleBoxFlat\" id=\"sb2\"]",
            "bg_color = Color(1, 1, 1, 1)",
            "",
            "[node name=\"Root\" type=\"Control\"]",
            "__entry_point = \"Test.OriginalEntry\"",
            "anchor_right = 1.0",
            "script = ExtResource(\"1_script\")",
            "",
            "[node name=\"Panel\" type=\"Panel\" parent=\".\"]",
            "theme_override_styles/panel = SubResource(\"sb1\")",
            "",
            "[node name=\"Child\" type=\"Label\" parent=\"Panel\"]",
            "text = \"Original\"",
            "tooltip_text = \"Tip\"",
            "icon = ExtResource(\"1_tex\")",
            "",
            "[node name=\"Btn\" type=\"Button\" parent=\".\"]",
            "",
            "[node name=\"RefHolder\" type=\"Node\" parent=\".\" node_paths=PackedStringArray(\"target\")]",
            "custom = NodePath(\"../Panel/Child\")",
            "abs = NodePath(\"/root/Abs\")",
            "label = \"Panel/Child\"",
            "target = NodePath(\"../Panel/Child\")",
            "",
            "[connection signal=\"pressed\" from=\"Btn\" to=\".\" method=\"_on_pressed\"]",
            "[connection signal=\"toggled\" from=\"Btn\" to=\".\" method=\"_on_toggled\" flags=1]",
            "[connection signal=\"visibility_changed\" from=\"Panel/Child\" to=\".\" method=\"_on_child_vis\"]",
        };

        [Fact]
        public void OverrideProperty_MergesAndKeepsOthers()
        {
            var scene = Apply(Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"Child\" parent=\"Panel\"]",
                "text = \"Changed\""));

            var child = Find(scene, "Panel/Child");
            Assert.Equal("Changed", child.Properties["text"]);
            Assert.Equal("Tip", child.Properties["tooltip_text"]);
            Assert.True(child.Properties.ContainsKey("icon"));
            Assert.Equal("text", child.PropertyOrder[0]);
        }

        [Fact]
        public void OverrideProperty_NewKeyAppendedToOrder()
        {
            var scene = Apply(Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"Btn\" parent=\".\"]",
                "disabled = true"));

            var btn = Find(scene, "Btn");
            Assert.Equal(true, btn.Properties["disabled"]);
            Assert.Equal("disabled", btn.PropertyOrder.Last());
        }

        [Fact]
        public void OverrideRoot_MergesProperties()
        {
            var scene = Apply(Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "anchor_bottom = 1.0"));

            var root = Find(scene, "Root");
            Assert.Equal(1f, root.Properties["anchor_bottom"]);
            Assert.Equal(1f, root.Properties["anchor_right"]);
        }

        [Fact]
        public void AddNode_UnderExistingParent()
        {
            var scene = Apply(Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"Added\" type=\"Label\" parent=\"Panel\"]",
                "text = \"New\""));

            var added = Find(scene, "Panel/Added");
            Assert.NotNull(added);
            Assert.Equal("Label", added.Type);
            Assert.Equal("Panel", added.Parent);
            Assert.Equal("New", added.Properties["text"]);
        }

        [Fact]
        public void AddNode_DuplicatePath_Skipped()
        {
            var scene = Apply(Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"Panel\" type=\"Panel\" parent=\".\"]"));

            Assert.Single(scene.Nodes.Where(n => PathOf(n) == "Panel"));
            Assert.Equal("Panel", Find(scene, "Panel").Type);
        }

        [Fact]
        public void DeleteNodes_CascadesDescendantsConnectionsAndNodePaths()
        {
            var scene = Apply(Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "metadata/_patch_delete_nodes = PackedStringArray(\"Panel\")"));

            Assert.Null(Find(scene, "Panel"));
            Assert.Null(Find(scene, "Panel/Child"));
            Assert.DoesNotContain(scene.Connections, c => c.From == "Panel/Child");

            var refHolder = Find(scene, "RefHolder");
            Assert.False(refHolder.NodePathVars.ContainsKey("target"));
            Assert.False(refHolder.Properties.ContainsKey("custom"));
            Assert.True(refHolder.Properties.ContainsKey("abs"));
            Assert.True(refHolder.Properties.ContainsKey("label"));
        }

        [Fact]
        public void DeleteNodesMask_RemovesAllDescendantsButKeepsRoot()
        {
            var scene = Apply(Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "metadata/_patch_delete_nodes = PackedStringArray(\"*\")"));

            Assert.Single(scene.Nodes);
            Assert.Equal("Root", scene.Nodes[0].Name);
            Assert.Empty(scene.Connections);
        }

        [Fact]
        public void DeleteRoot_RejectedAndSceneUnchanged()
        {
            var target = Target();
            var patch = Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "metadata/_patch_delete = true");

            var ok = new ScenePatchApplier().ApplyAll(target, new[] { patch });

            Assert.False(ok);
            Assert.NotNull(Find(target, "Panel"));
        }

        [Fact]
        public void RemoveProperty_ExactKey()
        {
            var scene = Apply(Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"Child\" parent=\"Panel\"]",
                "metadata/_patch_remove = \"tooltip_text\""));

            var child = Find(scene, "Panel/Child");
            Assert.False(child.Properties.ContainsKey("tooltip_text"));
            Assert.False(child.PropertyOrder.Contains("tooltip_text"));
            Assert.True(child.Properties.ContainsKey("text"));
        }

        [Fact]
        public void RemoveProperty_Mask()
        {
            var scene = Apply(Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"Panel\" parent=\".\"]",
                "metadata/_patch_remove = PackedStringArray(\"theme_override_*\")"));

            var panel = Find(scene, "Panel");
            Assert.False(panel.Properties.ContainsKey("theme_override_styles/panel"));
        }

        [Fact]
        public void RemoveProperty_AllThenSet_LeavesOnlyNewValue()
        {
            var scene = Apply(Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"Child\" parent=\"Panel\"]",
                "metadata/_patch_remove = \"*\"",
                "text = \"Only\""));

            var child = Find(scene, "Panel/Child");
            Assert.Single(child.Properties);
            Assert.Equal("Only", child.Properties["text"]);
        }

        [Fact]
        public void Groups_AddAndRemove()
        {
            var scene = Apply(Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"Btn\" parent=\".\"]",
                "metadata/_patch_add_groups = PackedStringArray(\"g1\", \"g2\")",
                "metadata/_patch_remove_groups = PackedStringArray(\"g1\")"));

            var btn = Find(scene, "Btn");
            Assert.Equal(new[] { "g2" }, btn.Groups);
        }

        [Fact]
        public void Groups_HeaderReplaces()
        {
            var scene = Apply(Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"Btn\" parent=\".\" groups=[\"replaced\"]]"));

            Assert.Equal(new[] { "replaced" }, Find(scene, "Btn").Groups);
        }

        [Fact]
        public void Connections_RemoveByFlagsSelector()
        {
            var scene = Apply(Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "metadata/_patch_remove_connections = PackedStringArray(\"from=Btn;signal=toggled;flags=1\")"));

            Assert.Equal(2, scene.Connections.Count);
            Assert.DoesNotContain(scene.Connections, c => c.Signal == "toggled");
            Assert.Contains(scene.Connections, c => c.Signal == "pressed");
        }

        [Fact]
        public void Connections_RemoveByToSelector()
        {
            var scene = Apply(Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "metadata/_patch_remove_connections = PackedStringArray(\"to=.;signal=visibility_changed\")"));

            Assert.DoesNotContain(scene.Connections, c => c.Signal == "visibility_changed");
            Assert.Equal(2, scene.Connections.Count);
        }

        [Fact]
        public void Connections_Add()
        {
            var scene = Apply(Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[connection signal=\"pressed\" from=\"Btn\" to=\".\" method=\"_on_extra\"]"));

            Assert.Equal(4, scene.Connections.Count);
            Assert.Contains(scene.Connections, c => c.Method == "_on_extra");
        }

        [Fact]
        public void ResourceCollision_RemappedWithNewId()
        {
            var scene = Apply(Patch(
                "[ext_resource type=\"Texture2D\" path=\"res://tex/patched.png\" id=\"1_tex\"]",
                "",
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"Child\" parent=\"Panel\"]",
                "icon = ExtResource(\"1_tex\")"));

            var remapped = scene.ExtResources.FirstOrDefault(e => e.Id == "1_tex_p1");
            Assert.NotNull(remapped);
            Assert.Equal("res://tex/patched.png", remapped.Path);

            var icon = (TscnPropertyValue)Find(scene, "Panel/Child").Properties["icon"];
            Assert.Equal("1_tex_p1", icon.RefId);
        }

        [Fact]
        public void ResourceSamePath_ReusedWithoutDuplicate()
        {
            var scene = Apply(Patch(
                "[ext_resource type=\"Texture2D\" path=\"res://tex/original.png\" id=\"9_same\"]",
                "",
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"Child\" parent=\"Panel\"]",
                "icon = ExtResource(\"9_same\")"));

            Assert.Equal(4, scene.ExtResources.Count);
            var icon = (TscnPropertyValue)Find(scene, "Panel/Child").Properties["icon"];
            Assert.Equal("1_tex", icon.RefId);
        }

        [Fact]
        public void SubResourceDelete_ReferencedKept_UnreferencedRemoved()
        {
            var scene = Apply(Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "",
                "[sub_resource type=\"StyleBoxFlat\" id=\"sb1\"]",
                "metadata/_patch_delete = true",
                "",
                "[sub_resource type=\"StyleBoxFlat\" id=\"sb2\"]",
                "metadata/_patch_delete = true"));

            Assert.Contains(scene.SubResources, s => s.Id == "sb1");
            Assert.DoesNotContain(scene.SubResources, s => s.Id == "sb2");
        }

        [Fact]
        public void Gc_RunsAfterAllLayers_ResourceNeededByLaterLayerSurvives()
        {
            var gcLayer = Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "metadata/_patch_gc = true");

            var addLayer = Patch(
                "[ext_resource type=\"Texture2D\" path=\"res://tex/keep.png\" id=\"2_tex\"]",
                "",
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"Added\" type=\"Label\" parent=\".\"]",
                "icon = ExtResource(\"2_tex\")");

            var scene = Apply(gcLayer, addLayer);

            Assert.Contains(scene.ExtResources, e => e.Id == "1_tex");
            Assert.Contains(scene.ExtResources, e => e.Id == "2_tex");
            Assert.DoesNotContain(scene.ExtResources, e => e.Id == "4_tex");

            var icon = (TscnPropertyValue)Find(scene, "Added").Properties["icon"];
            Assert.Equal("2_tex", icon.RefId);
        }

        [Fact]
        public void Layers_LaterOverridesAndAdds()
        {
            var first = Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"Child\" parent=\"Panel\"]",
                "text = \"First\"");

            var second = Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"Child\" parent=\"Panel\"]",
                "text = \"Second\"",
                "",
                "[node name=\"Extra\" type=\"Label\" parent=\".\"]");

            var scene = Apply(first, second);

            Assert.Equal("Second", Find(scene, "Panel/Child").Properties["text"]);
            Assert.NotNull(Find(scene, "Extra"));
        }

        [Fact]
        public void EntryPoint_Overridden()
        {
            var scene = Apply(Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "__entry_point = \"Test.PatchedEntry\""));

            Assert.Equal("Test.PatchedEntry", scene.EntryPointClass);
        }

        [Fact]
        public void Index_PlacesAddedNodeFirst()
        {
            var scene = Apply(Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"First\" type=\"Label\" parent=\".\" index=\"0\"]"));

            var paths = scene.Nodes.Select(PathOf).ToList();
            Assert.Equal("First", paths[1]);
            Assert.Equal("Panel", paths[2]);
        }

        [Fact]
        public void Index_MovesOverriddenNode()
        {
            var scene = Apply(Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"RefHolder\" parent=\".\" index=\"0\"]"));

            var paths = scene.Nodes.Select(PathOf).ToList();
            Assert.Equal("RefHolder", paths[1]);
        }

        [Fact]
        public void CachedPatch_IsNotMutatedBetweenApplies()
        {
            var patch = Patch(
                "[ext_resource type=\"Texture2D\" path=\"res://tex/patched.png\" id=\"1_tex\"]",
                "",
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"Child\" parent=\"Panel\"]",
                "icon = ExtResource(\"1_tex\")");

            var first = Apply(patch);
            var second = Apply(patch);

            Assert.Equal("1_tex_p1", ((TscnPropertyValue)Find(first, "Panel/Child").Properties["icon"]).RefId);
            Assert.Equal("1_tex_p1", ((TscnPropertyValue)Find(second, "Panel/Child").Properties["icon"]).RefId);

            var sourceIcon = (TscnPropertyValue)patch.Scene.Nodes.First(n => n.Name == "Child").Properties["icon"];
            Assert.Equal("1_tex", sourceIcon.RefId);
        }

        [Fact]
        public void UnknownOverridePath_IsSkipped()
        {
            var target = Target();
            var patch = Patch(
                "[node name=\"Root\" instance=ExtResource(\"1_target\")]",
                "[node name=\"Missing\" parent=\".\"]",
                "text = \"x\"");

            var ok = new ScenePatchApplier().ApplyAll(target, new[] { patch });

            Assert.True(ok);
            Assert.DoesNotContain(target.Nodes, n => n.Name == "Missing");
        }

        private static ParsedScene Target() => new TscnParser().Parse(string.Join("\n", TargetLines));

        private static ParsedScene Apply(params ScenePatch[] patches)
        {
            var target = Target();
            var ok = new ScenePatchApplier().ApplyAll(target, patches);
            Assert.True(ok, "ApplyAll should succeed for a valid patch");
            return target;
        }

        private static ScenePatch Patch(params string[] bodyLines)
        {
            var text = "[gd_scene format=3]\n\n"
                + "[ext_resource type=\"PackedScene\" path=\"res://scenes/target.tscn\" id=\"1_target\"]\n\n"
                + string.Join("\n", bodyLines);
            Assert.True(ScenePatchParser.TryParse(text, "test_mod", "patches/scenes/target.tscn", out var patch, out var error), error);
            return patch;
        }

        private static ParsedNode Find(ParsedScene scene, string path)
            => scene.Nodes.FirstOrDefault(n => PathOf(n) == path);

        private static string PathOf(ParsedNode node)
        {
            if (string.IsNullOrEmpty(node.Parent) || node.Parent == "." || node.Parent == "..")
                return node.Name;
            return node.Parent.TrimEnd('/') + "/" + node.Name;
        }
    }
}
