"""Public CLI contracts: syntax, source policy, context isolation and exit codes."""

import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

DRIVER = Path(__file__).resolve().parents[1] / "check_style.py"


class CheckStyleTests(unittest.TestCase):
    """Each case owns its complete miniature source tree and removes it afterwards."""

    def setUp(self):
        self.workspace = tempfile.TemporaryDirectory(prefix="cthangover-style-test-")
        self.addCleanup(self.workspace.cleanup)
        self.root = Path(self.workspace.name)

    def write(self, name, text):
        path = self.root / name
        path.parent.mkdir(parents=True, exist_ok=True)
        path.write_text(text, encoding="utf-8")
        return path

    def run_check(self, *arguments, expected=None, stdin=None):
        result = subprocess.run(
            [sys.executable, str(DRIVER), "--root", str(self.root), "--json", str(self.root / "report.json"), *arguments],
            input=stdin, capture_output=True, text=True,
        )
        if expected is not None:
            self.assertEqual(expected, result.returncode, result.stderr + result.stdout)
        self.assertIn(result.returncode, (0, 1), result.stderr)
        payload = json.loads((self.root / "report.json").read_text())
        return payload, {item["rule"] for item in payload["issues"]}

    def test_clean_code_and_literal_semicolons(self):
        self.write("sample.cs", '''using System;
/// <summary>Returns literal delimiters without executing any game code.</summary>
public class Sample
{
    /// <summary>Retains delimiters in text passed to callers.</summary>
    public string Read()
    {
        // These delimiters ; ; are prose.
        var text = @"; ;";
        return text + "; ;";
    }
}
''')
        payload, rules = self.run_check("sample.cs", expected=0)
        self.assertEqual(1, payload["checkedFiles"])
        self.assertEqual(set(), rules)

    def test_for_header_and_auto_accessors_are_not_instructions(self):
        self.write("sample.cs", '''internal class Sample
{
    /// <summary>Caller-owned counter value.</summary>
    public int Count { get; set; }

    internal void Run()
    {
        for (var i = 0; i < 3; i++)
        {
            Count++;
        }
    }
}
''')
        self.run_check("sample.cs", expected=0)

    def test_multiple_statements_have_editor_coordinates(self):
        self.write("sample.cs", "internal class Sample\n{\n    int a; int b;\n}\n")
        payload, rules = self.run_check("sample.cs", expected=1)
        self.assertEqual({"MULTIPLE_MEMBERS"}, rules)
        item = payload["issues"][0]
        self.assertEqual(("sample.cs", 3, 12), (item["path"], item["line"], item["column"]))

    def test_inline_and_expression_bodies(self):
        self.write("sample.cs", "internal class Sample\n{\n    int A() { return 1; }\n    int B() => 1;\n}\n")
        payload, rules = self.run_check("sample.cs", expected=1)
        self.assertTrue({"ONE_LINE_BODY", "ALLMAN_BRACES"}.issubset(rules))
        self.assertEqual({3, 4}, {item["line"] for item in payload["issues"] if item["rule"] == "ONE_LINE_BODY"})

    def test_public_docs_include_members_and_attributes(self):
        self.write("sample.cs", '''/// <summary>Visible type contract.</summary>
[System.Serializable]
public class Sample
{
    /// <summary>Nonempty summary before the attribute.</summary>
    [System.Obsolete]
    public int A;

    public int B;
}
''')
        payload, rules = self.run_check("sample.cs", expected=1)
        self.assertEqual({"PUBLIC_DOCUMENTATION"}, rules)
        self.assertEqual(9, payload["issues"][0]["line"])

    def test_implicit_interface_members_and_enum_values_require_docs(self):
        self.write("api.cs", "internal interface Api\n{\n    void Run();\n}\n")
        self.write("choice.cs", "/// <summary>Stable choices.</summary>\npublic enum Choice\n{\n    First\n}\n")
        payload, rules = self.run_check("api.cs", "choice.cs", expected=1)
        self.assertEqual({"PUBLIC_DOCUMENTATION"}, rules)
        self.assertEqual(2, len(payload["issues"]))

    def test_multiple_types_using_placement_and_fields(self):
        self.write("sample.cs", '''namespace Demo
{
    using System;

    internal class First
    {
        int a, b;
    }

    internal class Second
    {
    }
}
''')
        _, rules = self.run_check("sample.cs", expected=1)
        self.assertEqual({"ONE_TYPE_PER_FILE", "USING_PLACEMENT", "SINGLE_FIELD"}, rules)

    def test_lambdas_anonymous_delegates_and_local_functions(self):
        self.write("sample.cs", '''internal class Sample
{
    void Run()
    {
        System.Action first = () => Call();
        System.Action second = delegate
        {
            Call();
        };
        void Local()
        {
            Call();
        }
    }
}
''')
        payload, rules = self.run_check("sample.cs", expected=1)
        self.assertEqual({"ANONYMOUS_FUNCTION", "LOCAL_FUNCTION"}, rules)
        self.assertEqual(3, len(payload["issues"]))

    def test_raw_literal_interiors_keep_their_width_and_delimiters(self):
        self.write("sample.cs", 'internal class Sample\n{\n    string text = """\n' + "        " + ";" * 200 + '\n        """;\n}\n')
        self.run_check("sample.cs", expected=0)

    def test_long_code_line(self):
        self.write("sample.cs", "internal class Sample\n{\n    // " + "word " * 30 + "\n}\n")
        _, rules = self.run_check("sample.cs", expected=1)
        self.assertEqual({"LINE_LENGTH"}, rules)

    def test_logging_only_restricted_inside_mods(self):
        code = "internal class Sample\n{\n    void Run()\n    {\n        Console.WriteLine(1);\n        GD.Print(2);\n    }\n}\n"
        self.write("tool.cs", code)
        self.write("mods/demo_base/manifest.json", '{"id":"demo_base"}')
        self.write("mods/demo_base/sample.cs", code)
        self.run_check("tool.cs", expected=0)
        payload, rules = self.run_check("mods/demo_base", expected=1)
        self.assertEqual({"GAME_LOGGER"}, rules)
        self.assertEqual(2, len(payload["issues"]))

    def test_only_canonical_logger_implementation_owns_direct_output(self):
        code = '''namespace Cthangover.Core.Utils
{
    internal static class GameLogger
    {
        internal static void Write()
        {
            Console.WriteLine("record");
            GD.Print("record");
        }
    }
}
'''
        self.write("Core/Utils/GameLogger.cs", code)
        self.run_check("Core/Utils/GameLogger.cs", expected=0)
        self.write("mods/demo_base/manifest.json", '{"id":"demo_base"}')
        self.write("mods/demo_base/GameLogger.cs", code)
        payload, rules = self.run_check("mods/demo_base/GameLogger.cs", expected=1)
        self.assertEqual({"GAME_LOGGER"}, rules)
        self.assertEqual(2, len(payload["issues"]))
        self.write("Core/Utils/GameLogger.cs", code.replace("class GameLogger", "class OtherSink"))
        payload, rules = self.run_check("Core/Utils/GameLogger.cs", expected=1)
        self.assertEqual({"GAME_LOGGER"}, rules)
        self.assertEqual(2, len(payload["issues"]))

    def test_widget_inheritance_uses_unselected_context(self):
        self.write("mods/ui_base/src/Base/Lists/ModWidget.cs", "namespace Cthangover.Mods.Interface.Base.Lists { internal class ModWidget {} }")
        self.write("mods/ui_base/widget.cs", "using Cthangover.Mods.Interface.Base.Lists; internal class ToolWindow : ModWidget {}")
        self.write("mods/demo_editor/manifest.json", '{"id":"demo_editor","depends":["ui_base"]}')
        self.write("mods/demo_editor/sample.cs", '''internal class Sample : ToolWindow
{
    void Build()
    {
        var label = new Label();
    }
}
''')
        self.run_check("mods/demo_editor", expected=0)

    def test_tool_control_requires_widget(self):
        self.write("mods/demo_editor/manifest.json", '{"id":"demo_editor","depends":["ui_base"]}')
        self.write("mods/demo_editor/sample.cs", "internal class Sample\n{\n    Label label = new Label();\n}\n")
        _, rules = self.run_check("mods/demo_editor", expected=1)
        self.assertEqual({"MOD_WIDGET"}, rules)

    def test_game_skeleton_and_data_rows(self):
        self.write("mods/demo_base/manifest.json", '{"id":"demo_base"}')
        source = '''internal class Sample : Control
{
    void Rows(string[] names)
    {
        foreach (var name in names)
        {
            var row = new Label();
        }
    }
}
'''
        self.write("mods/demo_base/sample.cs", source)
        _, rules = self.run_check("mods/demo_base", expected=1)
        self.assertEqual({"GAME_UI_PREFAB"}, rules)
        self.write("mods/demo_base/screen.tscn", "[gd_scene format=3]\n[node name=\"Screen\" type=\"Control\"]\n")
        self.run_check("mods/demo_base", expected=0)

    def test_static_game_controls_still_rejected_with_prefab(self):
        self.write("mods/demo_base/manifest.json", '{"id":"demo_base"}')
        self.write("mods/demo_base/screen.tscn", "[gd_scene format=3]\n")
        self.write("mods/demo_base/sample.cs", "internal class Sample\n{\n    Label label = new Label();\n}\n")
        _, rules = self.run_check("mods/demo_base", expected=1)
        self.assertEqual({"GAME_UI_PREFAB"}, rules)

    def test_changed_paths_ignore_deleted_files_and_unselected_diagnostics(self):
        self.write("selected.cs", "internal class Selected\n{\n}\n")
        self.write("dirty.cs", "public class Dirty {}")
        paths = self.write("changed.txt", "  selected.cs  \ndeleted.cs\nnotes.md\n")
        payload, _ = self.run_check("--paths-file", str(paths), expected=0)
        self.assertEqual(1, payload["checkedFiles"])
        payload, _ = self.run_check("--paths-file", "-", stdin="selected.cs\n", expected=0)
        self.assertEqual(1, payload["checkedFiles"])

    def test_comment_cannot_impersonate_documentation(self):
        self.write("sample.cs", "// <summary>Plain comment.</summary>\npublic class Sample\n{\n}\n")
        _, rules = self.run_check("sample.cs", expected=1)
        self.assertEqual({"PUBLIC_DOCUMENTATION"}, rules)

    def test_utility_builder_accepts_borrowed_widget_parameter(self):
        self.write("mods/demo_editor/manifest.json", '{"id":"demo_editor","depends":["ui_base"]}')
        self.write("mods/ui_base/src/Base/Lists/ModWidget.cs", "namespace Cthangover.Mods.Interface.Base.Lists { internal class ModWidget {} }")
        self.write("mods/demo_editor/sample.cs", "using Cthangover.Mods.Interface.Base.Lists;\ninternal class Sample\n{\n    void Build(ModWidget root)\n    {\n        var label = new Label();\n        root.AddChild(label);\n    }\n}\n")
        self.run_check("mods/demo_editor", expected=0)

    def test_syntax_error_is_a_finding(self):
        self.write("broken.cs", "internal class")
        _, rules = self.run_check("broken.cs", expected=1)
        self.assertIn("SYNTAX", rules)

    def test_initializers_and_anonymous_types(self):
        self.write("sample.cs", "internal class Sample\n{\n    void Build()\n    {\n        var first = new Value { A = 1, B = 2 };\n        var other = new { A = 1 };\n    }\n}\n")
        _, rules = self.run_check("sample.cs", expected=1)
        self.assertEqual({"MULTIPLE_INITIALIZERS", "ANONYMOUS_TYPE"}, rules)

    def test_empty_xml_summary_is_not_documentation(self):
        self.write("sample.cs", "/// <summary> </summary>\npublic class Sample\n{\n}\n")
        _, rules = self.run_check("sample.cs", expected=1)
        self.assertEqual({"PUBLIC_DOCUMENTATION"}, rules)

    def test_logging_alias_cannot_bypass_game_logger(self):
        self.write("mods/demo_base/manifest.json", '{"id":"demo_base"}')
        self.write("mods/demo_base/sample.cs", "using Output = System.Console;\ninternal class Sample\n{\n    void Log()\n    {\n        Output.WriteLine(1);\n    }\n}\n")
        _, rules = self.run_check("mods/demo_base", expected=1)
        self.assertEqual({"GAME_LOGGER"}, rules)

    def test_game_screen_needs_prefab_even_without_control_creation(self):
        self.write("mods/demo_base/manifest.json", '{"id":"demo_base"}')
        self.write("mods/demo_base/sample.cs", "internal class Screen : CanvasLayer\n{\n}\n")
        _, rules = self.run_check("mods/demo_base", expected=1)
        self.assertEqual({"GAME_UI_PREFAB"}, rules)

    def test_partial_type_uses_documentation_on_another_part(self):
        self.write("first.cs", "/// <summary>Shares the implementation across dedicated files.</summary>\npublic partial class Sample\n{\n}\n")
        self.write("second.cs", "public partial class Sample\n{\n}\n")
        self.run_check("first.cs", "second.cs", expected=0)

    def test_changed_path_json_array_and_direct_arguments(self):
        self.write("sample.cs", "internal class Sample\n{\n}\n")
        paths = self.write("paths.json", '["sample.cs", "deleted.cs"]')
        payload, _ = self.run_check("--paths-file", str(paths), expected=0)
        self.assertEqual(1, payload["checkedFiles"])
        payload, _ = self.run_check("--changed-paths", "sample.cs", expected=0)
        self.assertEqual(1, payload["checkedFiles"])

    def test_switch_arms_are_not_lambdas(self):
        self.write("sample.cs", "internal class Sample\n{\n    int Choose(int value)\n    {\n        return value switch\n        {\n            0 => 1,\n            _ => 2\n        };\n    }\n}\n")
        self.run_check("sample.cs", expected=0)

    def test_short_literal_collections_are_data_not_object_initializers(self):
        self.write("sample.cs", 'internal class Sample\n{\n    string[] labels = { "one", "two" };\n    float[] position = { -1, 0, 1 };\n}\n')
        self.run_check("sample.cs", expected=0)

    def test_editor_integration_borrows_its_utility_manifest_policy(self):
        self.write("mods/ui_base/src/Base/Lists/ModWidget.cs", "namespace Cthangover.Mods.Interface.Base.Lists { internal class ModWidget {} }")
        self.write("mods/demo_base/manifest.json", '{"id":"demo_base","categories":["engine"],"depends":["ui_base"]}')
        self.write("mods/demo_base/integrations/editor/manifest.json", '{"id":"demo_base__editor"}')
        self.write("mods/demo_base/integrations/editor/sample.cs", "using Cthangover.Mods.Interface.Base.Lists;\ninternal class Panel : ModWidget\n{\n    void Build()\n    {\n        var label = new Label();\n    }\n}\n")
        self.run_check("mods/demo_base", expected=0)

    def test_missing_changed_list_is_invocation_error(self):
        result = subprocess.run(
            [sys.executable, str(DRIVER), "--root", str(self.root), "--changed-paths", "missing.txt"],
            capture_output=True, text=True,
        )
        self.assertEqual(2, result.returncode)
        self.assertIn("Style check failed:", result.stderr)


if __name__ == "__main__":
    unittest.main()
