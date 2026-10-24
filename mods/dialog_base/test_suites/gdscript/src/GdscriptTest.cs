using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.DialogBase.Tests
{
    /// <summary>Entry point of the GDScript itest; the suite is mounted only with <c>--itest=gdscript</c>.</summary>
    public class GdscriptTestInitializer : IModInitializer
    {
        private static bool queued;

        public void OnModLoaded(string modId) { }

        public void OnModResourcesReady()
        {
            if (queued || !OS.GetCmdlineUserArgs().Contains("--itest=gdscript"))
                return;
            queued = true;
            GameLogger.Log("TEST", "GdscriptTest: activating");
            ModRoot.AddDeferred(new GdscriptTestDriverNode());
        }
    }

    /// <summary>
    /// Checks the GDScript mod pipeline end to end on the suite's own
    /// <c>scripts/*.gd</c>: every script with <c>get_name()</c> + <c>run()</c>
    /// is a registered scenario action, and every <c>get_condition_name()</c>
    /// script is a working condition — <c>gds.is_rain</c> and
    /// <c>gds.flag_equals("victory")</c> are evaluated through the same
    /// <see cref="ConditionParser"/> scenarios use, against flags that must
    /// make them true and false. Prints <c>[TEST_RESULT] name=gdscript</c>.
    /// </summary>
    public partial class GdscriptTestDriverNode : Node
    {
        private const string ResultName = "gdscript";
        private const string SuiteModId = "dialog_base__gdscript";
        private const double TimeoutSeconds = 60;
        private static readonly Regex ReturnedName = new(@"func\s+(get_name|get_condition_name)\s*\(\s*\)\s*:\s*\n\s*return\s+""([^""]+)""", RegexOptions.Compiled);

        private int passed;
        private int failed;
        private double elapsed;
        private bool done;

        public override void _Process(double delta)
        {
            if (done)
                return;
            elapsed += delta;
            if (elapsed > TimeoutSeconds)
            {
                Check("boot finished in time", false);
                Finish();
                return;
            }

            if (!Boot.IsReady)
                return;

            try
            {
                Run();
            }
            catch (Exception ex)
            {
                Check($"no exception ({ex.GetType().Name}: {ex.Message})", false);
            }

            Finish();
        }

        private void Run()
        {
            var actions = new List<string>();
            var conditions = new List<string>();
            foreach (var file in ModManager.Instance.ListFiles(SuiteModId, "scripts").Where(f => f.EndsWith(".gd", StringComparison.Ordinal)))
            {
                var text = (ModManager.Instance.ReadFileText(SuiteModId, file) ?? string.Empty).Replace("\r\n", "\n");
                foreach (Match match in ReturnedName.Matches(text))
                {
                    if (match.Groups[1].Value == "get_condition_name")
                        conditions.Add(match.Groups[2].Value);
                    else if (text.Contains("func run("))
                        actions.Add(match.Groups[2].Value);
                }
            }

            Check($"suite scripts found ({actions.Count} action(s), {conditions.Count} condition(s))", actions.Count > 0 && conditions.Count > 0);
            foreach (var name in actions)
                Check($"action '{name}' registered", ScenarioActionFactory.Instance.Get(name) != null);

            Expect("gds.is_rain", new() { ["weather"] = "rain" }, true);
            Expect("gds.is_rain", new() { ["weather"] = "sun" }, false);
            Expect("gds.flag_equals(\"victory\")", new() { ["custom_flag"] = "victory" }, true);
            Expect("gds.flag_equals(\"victory\")", new() { ["custom_flag"] = "defeat" }, false);
            Expect("gds.is_rain && gds.flag_equals(\"victory\")", new() { ["weather"] = "rain", ["custom_flag"] = "victory" }, true);
        }

        private void Expect(string condition, Dictionary<string, string> flags, bool expected)
        {
            var node = ConditionParser.Parse(condition);
            var actual = node != null && node.Evaluate(new EvaluationContext { Flags = flags });
            Check($"'{condition}' with {string.Join(",", flags.Select(f => $"{f.Key}={f.Value}"))} → {expected}" + (actual == expected ? string.Empty : $" (got {actual})"), actual == expected);
        }

        private void Check(string what, bool ok)
        {
            if (ok)
                passed++;
            else
                failed++;
            GameLogger.Log("TEST", $"  [{(ok ? "PASS" : "FAIL")}] {what}");
        }

        private void Finish()
        {
            done = true;
            GameLogger.Log("TEST", $"[TEST_RESULT] name={ResultName} passed={passed} failed={failed}");
            GetTree().Quit(failed == 0 ? 0 : 1);
        }
    }
}
