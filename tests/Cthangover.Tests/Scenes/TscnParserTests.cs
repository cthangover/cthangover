using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Scenes.TscnParsing;

namespace Engine.Tests.Scenes
{
    public class TscnLexerTests
    {
        [Fact]
        public void Tokenize_Empty()
        {
            var lexer = new TscnLexer();
            var tokens = lexer.Tokenize("");
            Assert.Empty(tokens);
        }

        [Fact]
        public void Tokenize_CommentLine()
        {
            var lexer = new TscnLexer();
            var tokens = lexer.Tokenize("; comment line");
            Assert.Empty(tokens);
        }

        [Fact]
        public void Tokenize_GdSceneHeader()
        {
            var lexer = new TscnLexer();
            var tokens = lexer.Tokenize("[gd_scene format=3]");
            Assert.Single(tokens);
            Assert.Equal(TscnTokenType.SectionHeader, tokens[0].Type);
            Assert.Equal("gd_scene", tokens[0].SectionName);
            Assert.Equal("3", tokens[0].SectionAttrs["format"]);
        }

        [Fact]
        public void Tokenize_ExtResource()
        {
            var lexer = new TscnLexer();
            var tokens = lexer.Tokenize("[ext_resource type=\"Script\" path=\"res://Core/MyScript.cs\" id=\"1_script\"]");

            Assert.Single(tokens);
            var header = tokens[0];
            Assert.Equal("ext_resource", header.SectionName);
            Assert.Equal("Script", header.SectionAttrs["type"]);
            Assert.Equal("res://Core/MyScript.cs", header.SectionAttrs["path"]);
            Assert.Equal("1_script", header.SectionAttrs["id"]);
        }

        [Fact]
        public void Tokenize_NodeHeader()
        {
            var lexer = new TscnLexer();
            var tokens = lexer.Tokenize("[node name=\"Root\" type=\"Control\"]");

            Assert.Single(tokens);
            var header = tokens[0];
            Assert.Equal("node", header.SectionName);
            Assert.Equal("Root", header.SectionAttrs["name"]);
            Assert.Equal("Control", header.SectionAttrs["type"]);
        }

        [Fact]
        public void Tokenize_NodeWithParent()
        {
            var lexer = new TscnLexer();
            var tokens = lexer.Tokenize("[node name=\"Child\" parent=\".\" type=\"TextureRect\"]");

            Assert.Single(tokens);
            var header = tokens[0];
            Assert.Equal("Child", header.SectionAttrs["name"]);
            Assert.Equal(".", header.SectionAttrs["parent"]);
        }

        [Fact]
        public void Tokenize_PropertyLine()
        {
            var lexer = new TscnLexer();
            var tokens = lexer.Tokenize("anchor_right = 1.0");

            Assert.Single(tokens);
            Assert.Equal(TscnTokenType.Property, tokens[0].Type);
            Assert.Equal("anchor_right", tokens[0].PropertyKey);
            Assert.Equal("1.0", tokens[0].PropertyValue);
        }

        [Fact]
        public void Tokenize_FullScene()
        {
            var source = @"
[gd_scene format=3]
[ext_resource type=""Script"" path=""res://test.cs"" id=""1""]

[node name=""Root"" type=""Control""]
anchor_right = 1.0
script = ExtResource(""1"")

[node name=""Child"" parent=""."" type=""Label""]
text = ""Hello""
".Trim();

            var lexer = new TscnLexer();
            var tokens = lexer.Tokenize(source);

            Assert.Equal(7, tokens.Count);
            Assert.Equal("gd_scene", tokens[0].SectionName);
            Assert.Equal("ext_resource", tokens[1].SectionName);
            Assert.Equal("node", tokens[2].SectionName);
            Assert.Equal(TscnTokenType.Property, tokens[3].Type);
            Assert.Equal(TscnTokenType.Property, tokens[4].Type);
            Assert.Equal("node", tokens[5].SectionName);
            Assert.Equal(TscnTokenType.Property, tokens[6].Type);
        }
    }

    public class TscnParserTests
    {
        [Fact]
        public void Parse_MinimalScene()
        {
            var source = @"
[gd_scene format=3]

[node name=""Root"" type=""Control""]
anchor_right = 1.0
".Trim();

            var parser = new TscnParser();
            var scene = parser.Parse(source);

            Assert.NotNull(scene);
            Assert.Equal("3", scene.Format);
            Assert.Single(scene.Nodes);
            Assert.Equal("Root", scene.Nodes[0].Name);
            Assert.Equal("Control", scene.Nodes[0].Type);
            Assert.Single(scene.Nodes[0].Properties);
            Assert.Equal(1.0f, scene.Nodes[0].Properties["anchor_right"]);
        }

        [Fact]
        public void Parse_WithExtResources()
        {
            var source = @"
[gd_scene format=3]
[ext_resource type=""Script"" path=""res://test.cs"" id=""1""]
[ext_resource type=""PackedScene"" path=""res://child.tscn"" id=""2""]

[node name=""Root"" type=""Control""]
script = ExtResource(""1"")

[node name=""Child"" parent=""."" instance=ExtResource(""2"")]
visible = false
".Trim();

            var parser = new TscnParser();
            var scene = parser.Parse(source);

            Assert.Equal(2, scene.ExtResources.Count);
            Assert.Equal("Script", scene.ExtResources[0].Type);
            Assert.Equal("res://test.cs", scene.ExtResources[0].Path);
            Assert.Equal("1", scene.ExtResources[0].Id);

            Assert.Equal("res://child.tscn", scene.ExtResources[1].Path);

            Assert.Equal(2, scene.Nodes.Count);
            Assert.Equal("ExtResource(\"1\")", scene.Nodes[0].ScriptRef);
            Assert.Equal("ExtResource(\"2\")", scene.Nodes[1].InstanceRef);
            Assert.Equal(false, scene.Nodes[1].Properties["visible"]);
        }

        [Fact]
        public void Parse_NodeHierarchy()
        {
            var source = @"
[gd_scene format=3]

[node name=""Root"" type=""Control""]

[node name=""Panel"" parent=""."" type=""Control""]

[node name=""Button"" parent=""Panel"" type=""Button""]
text = ""Click""
".Trim();

            var parser = new TscnParser();
            var scene = parser.Parse(source);

            Assert.Equal(3, scene.Nodes.Count);
            Assert.Equal("Root", scene.Nodes[0].Name);
            Assert.Null(scene.Nodes[0].Parent);

            Assert.Equal("Panel", scene.Nodes[1].Name);
            Assert.Equal(".", scene.Nodes[1].Parent);

            Assert.Equal("Button", scene.Nodes[2].Name);
            Assert.Equal("Panel", scene.Nodes[2].Parent);
        }

        [Fact]
        public void Parse_Groups()
        {
            var source = "[node name=\"Root\" type=\"Control\" groups=[\"test_group\"]]";

            var parser = new TscnParser();
            var scene = parser.Parse(source);

            Assert.Single(scene.Nodes);
            Assert.NotNull(scene.Nodes[0].Groups);
            Assert.Single(scene.Nodes[0].Groups);
            Assert.Equal("test_group", scene.Nodes[0].Groups[0]);
        }

        [Fact]
        public void Parse_Connections()
        {
            var source = @"
[gd_scene format=3]

[node name=""Root"" type=""Control""]

[node name=""Btn"" parent=""."" type=""Button""]

[connection signal=""pressed"" from=""Btn"" to=""."" method=""OnPressed""]
".Trim();

            var parser = new TscnParser();
            var scene = parser.Parse(source);

            Assert.Single(scene.Connections);
            Assert.Equal("pressed", scene.Connections[0].Signal);
            Assert.Equal("Btn", scene.Connections[0].From);
            Assert.Equal(".", scene.Connections[0].To);
            Assert.Equal("OnPressed", scene.Connections[0].Method);
        }

        [Fact]
        public void Parse_IgnoresNonScriptConnections()
        {
            // Connection with to="Light" should be ignored (not a script method)
            var source = @"
[connection signal=""finished"" from=""Anim"" to=""Light"" method=""_on_finished""]
[connection signal=""pressed"" from=""Btn"" to=""."" method=""OnPressed""]
".Trim();

            var parser = new TscnParser();
            var scene = parser.Parse(source);

            Assert.Single(scene.Connections);
            Assert.Equal("OnPressed", scene.Connections[0].Method);
        }

        [Fact]
        public void Parse_ConnectionFlags()
        {
            var source = "[connection signal=\"pressed\" from=\"Btn\" to=\".\" method=\"OnPressed\" flags=4]";

            var parser = new TscnParser();
            var scene = parser.Parse(source);

            Assert.Single(scene.Connections);
            Assert.Equal(4, scene.Connections[0].Flags);
        }

        [Fact]
        public void Parse_ExtResource_Texture2D()
        {
            var source = @"
[gd_scene format=3]

[ext_resource type=""Texture2D"" path=""res://backgrounds/cook_table.png"" id=""1_bg""]

[node name=""Root"" type=""Control""]
texture = ExtResource(""1_bg"")
".Trim();

            var parser = new TscnParser();
            var scene = parser.Parse(source);

            Assert.Single(scene.ExtResources);
            Assert.Equal("Texture2D", scene.ExtResources[0].Type);
            Assert.Equal("res://backgrounds/cook_table.png", scene.ExtResources[0].Path);
            Assert.Equal("1_bg", scene.ExtResources[0].Id);

            Assert.True(scene.Nodes[0].Properties.TryGetValue("texture", out var val));
            Assert.IsType<TscnPropertyValue>(val);
            Assert.Equal("1_bg", ((TscnPropertyValue)val).RefId);
        }

        [Fact]
        public void Parse_EntryPoint()
        {
            var source = @"
[gd_scene format=3]

[node name=""Root"" type=""Control""]
__entry_point = ""MyMod.EntryClass""
".Trim();

            var parser = new TscnParser();
            var scene = parser.Parse(source);

            Assert.Equal("MyMod.EntryClass", scene.EntryPointClass);
        }

        [Fact]
        public void Parse_SubResource()
        {
            var source = @"
[gd_scene format=3]

[sub_resource type=""StyleBoxFlat"" id=""style_1""]
bg_color = Color(0.1, 0.2, 0.3, 1)
".Trim();

            var parser = new TscnParser();
            var scene = parser.Parse(source);

            Assert.Single(scene.SubResources);
            Assert.Equal("StyleBoxFlat", scene.SubResources[0].Type);
            Assert.Equal("style_1", scene.SubResources[0].Id);
            Assert.True(scene.SubResources[0].Properties.ContainsKey("bg_color"));
        }

        [Fact]
        public void Parse_RealScene_DialogCanvas()
        {
            var source = @"
[gd_scene format=3]

[ext_resource type=""Script"" path=""res://Core/UI/Dialog/DialogCanvas.cs"" id=""1_canvas""]
[ext_resource type=""PackedScene"" path=""res://scenes/ui/dialog_box.tscn"" id=""2_dialogbox""]

[node name=""DialogCanvas"" type=""Control""]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
grow_horizontal = 2
grow_vertical = 2
script = ExtResource(""1_canvas"")

[node name=""DialogBox"" parent=""."" instance=ExtResource(""2_dialogbox"")]
".Trim();

            var parser = new TscnParser();
            var scene = parser.Parse(source);

            Assert.Equal(2, scene.ExtResources.Count);
            Assert.Equal(2, scene.Nodes.Count);
            Assert.Equal("DialogCanvas", scene.Nodes[0].Name);
            Assert.Equal("Control", scene.Nodes[0].Type);
            Assert.Equal("ExtResource(\"1_canvas\")", scene.Nodes[0].ScriptRef);
            Assert.True(scene.Nodes[0].Properties.ContainsKey("layout_mode"));
            Assert.True(scene.Nodes[0].Properties.ContainsKey("anchor_right"));

            Assert.Equal("DialogBox", scene.Nodes[1].Name);
            Assert.Equal(".", scene.Nodes[1].Parent);
            Assert.Equal("ExtResource(\"2_dialogbox\")", scene.Nodes[1].InstanceRef);
        }

        [Fact]
        public void Parse_ExtResourceInProperty_PackedScene()
        {
            var source = @"
[gd_scene format=3]

[ext_resource type=""PackedScene"" path=""res://scenes/ui/answer_box.tscn"" id=""2_answerbox""]

[node name=""DialogBox"" type=""Control""]
script = ExtResource(""1_canvas"")
answerBoxScene = ExtResource(""2_answerbox"")
".Trim();

            var parser = new TscnParser();
            var scene = parser.Parse(source);

            Assert.Single(scene.ExtResources);
            Assert.Equal("PackedScene", scene.ExtResources[0].Type);
            Assert.Equal("2_answerbox", scene.ExtResources[0].Id);

            Assert.Single(scene.Nodes);
            var node = scene.Nodes[0];
            Assert.True(node.Properties.ContainsKey("answerBoxScene"));

            var propValue = node.Properties["answerBoxScene"];
            Assert.IsType<TscnPropertyValue>(propValue);
            var pv = (TscnPropertyValue)propValue;
            Assert.True(pv.IsExtResourceRef);
            Assert.False(pv.IsSubResourceRef);
            Assert.Equal("2_answerbox", pv.RefId);
        }

        [Fact]
        public void Parse_MultipleExtResourceProperties()
        {
            var source = @"
[gd_scene format=3]

[ext_resource type=""PackedScene"" path=""res://a.tscn"" id=""1""]
[ext_resource type=""PackedScene"" path=""res://b.tscn"" id=""2""]

[node name=""Root"" type=""Control""]
prefabA = ExtResource(""1"")
prefabB = ExtResource(""2"")
NodePathRef = NodePath(""Some/Path"")
".Trim();

            var parser = new TscnParser();
            var scene = parser.Parse(source);

            var node = scene.Nodes[0];
            Assert.Equal(3, node.Properties.Count);

            var prefabA = (TscnPropertyValue)node.Properties["prefabA"];
            Assert.Equal("1", prefabA.RefId);

            var prefabB = (TscnPropertyValue)node.Properties["prefabB"];
            Assert.Equal("2", prefabB.RefId);

            var np = node.Properties["NodePathRef"];
            Assert.IsType<ParsedGodotValue>(np);
            Assert.Equal("NodePath", ((ParsedGodotValue)np).TypeName);
        }

        [Fact]
        public void Parse_RealScene_DialogBoxProperties()
        {
            var source = @"
[gd_scene format=3]

[ext_resource type=""Script"" path=""res://Core/UI/Dialog/DialogBox.cs"" id=""1_dialog""]
[ext_resource type=""PackedScene"" path=""res://scenes/ui/answer_box.tscn"" id=""2_answerbox""]

[node name=""DialogBox"" type=""Control"" node_paths=PackedStringArray(""textField"", ""titleField"", ""titleBody"", ""firstAvatar"", ""secondAvatar"")]
layout_mode = 3
script = ExtResource(""1_dialog"")
answerBoxScene = ExtResource(""2_answerbox"")
textField = NodePath(""DialogBody/TextField"")
titleField = NodePath(""TitleBody/TitleField"")
titleBody = NodePath(""TitleBody"")
firstAvatar = NodePath(""FirstAvatar"")
secondAvatar = NodePath(""SecondAvatar"")
".Trim();

            var parser = new TscnParser();
            var scene = parser.Parse(source);

            Assert.Single(scene.Nodes);
            var node = scene.Nodes[0];

            Assert.Equal("ExtResource(\"1_dialog\")", node.ScriptRef);

            var answerBox = (TscnPropertyValue)node.Properties["answerBoxScene"];
            Assert.True(answerBox.IsExtResourceRef);
            Assert.Equal("2_answerbox", answerBox.RefId);

            Assert.Equal(5, node.NodePathVars.Count);
            Assert.True(node.NodePathVars.ContainsKey("textField"));
            Assert.True(node.NodePathVars.ContainsKey("titleField"));
            Assert.True(node.NodePathVars.ContainsKey("titleBody"));
            Assert.True(node.NodePathVars.ContainsKey("firstAvatar"));
            Assert.True(node.NodePathVars.ContainsKey("secondAvatar"));
        }
    }
}
