namespace Cthangover.Mods.Stats.Tests
{
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Mods.Stats.Formulas;

    /// <summary>The formula language: what authors write in data must compute what a spreadsheet would.</summary>
    public class FormulaTests
    {
        private static double Eval(string text, Dictionary<string, double> vars = null)
            => Formula.Evaluate(text, new TestScope(vars));

        [ModTest("formula/precedence_and_parentheses")]
        static void PrecedenceAndParentheses(ModTestContext ctx)
        {
            ModAssert.CloseTo(14, Eval("2 + 3 * 4"), what: "* before +");
            ModAssert.CloseTo(20, Eval("(2 + 3) * 4"), what: "parentheses");
            ModAssert.CloseTo(1, Eval("10 % 3"), what: "remainder");
            ModAssert.CloseTo(-6, Eval("-2 * 3"), what: "unary minus");
            ModAssert.CloseTo(512, Eval("2 ^ 3 ^ 2"), what: "power is right-associative");
            ModAssert.CloseTo(2.5, Eval("5 / 2"), what: "real division");
        }

        [ModTest("formula/variables_with_dots_and_level")]
        static void VariablesWithDotsAndLevel(ModTestContext ctx)
        {
            var vars = new Dictionary<string, double> { ["a.atk"] = 12, ["b.def"] = 5, ["level"] = 3, ["str"] = 8 };
            ModAssert.CloseTo(19, Eval("a.atk * 2 - b.def", vars), what: "dotted names");
            ModAssert.CloseTo(14, Eval("STR + Level * 2", vars), what: "names ignore case");
            ModAssert.CloseTo(0, Eval("unknown + 0", vars), what: "unknown name reads as 0");
        }

        [ModTest("formula/comparisons_logic_and_conditional")]
        static void ComparisonsLogicAndConditional(ModTestContext ctx)
        {
            var vars = new Dictionary<string, double> { ["hp"] = 0, ["luk"] = 7 };
            ModAssert.CloseTo(1, Eval("luk >= 7", vars), what: ">=");
            ModAssert.CloseTo(0, Eval("luk != 7", vars), what: "!=");
            ModAssert.CloseTo(1, Eval("!hp", vars), what: "! of zero");
            ModAssert.CloseTo(0, Eval("hp > 0 && 100 / hp", vars), what: "&& short-circuits");
            ModAssert.CloseTo(30, Eval("luk > 5 ? 30 : 10", vars), what: "conditional true");
            ModAssert.CloseTo(10, Eval("luk > 9 ? 30 : luk > 8 ? 20 : 10", vars), what: "nested conditional");
        }

        [ModTest("formula/functions")]
        static void Functions(ModTestContext ctx)
        {
            ModAssert.CloseTo(1, Eval("max(1, 3 - 10)"), what: "max");
            ModAssert.CloseTo(2, Eval("min(9, 2, 5)"), what: "n-ary min");
            ModAssert.CloseTo(3, Eval("round(2.5)"), what: "round half away from zero");
            ModAssert.CloseTo(2, Eval("floor(2.9)"), what: "floor");
            ModAssert.CloseTo(10, Eval("clamp(42, 10, 0)"), what: "clamp with swapped bounds");
            ModAssert.CloseTo(4, Eval("if(1, 4, 5)"), what: "if");
            for (var i = 0; i < 50; i++)
            {
                var roll = Eval("rand(3, 5)");
                ModAssert.True(roll >= 3 && roll <= 5 && roll == System.Math.Floor(roll), $"rand(3,5) gave {roll}");
            }
            ModAssert.CloseTo(1, Eval("chance(100)"), what: "chance 100");
            ModAssert.CloseTo(0, Eval("chance(0)"), what: "chance 0");
        }

        [ModTest("formula/forgiving_arithmetic")]
        static void ForgivingArithmetic(ModTestContext ctx)
        {
            ModAssert.CloseTo(0, Eval("5 / 0"), what: "division by zero");
            ModAssert.CloseTo(0, Eval("5 % 0"), what: "remainder by zero");
            ModAssert.CloseTo(42, Formula.Evaluate("42", null), what: "plain number without a scope");
            ModAssert.CloseTo(7, Formula.Evaluate("", null, 7), what: "empty text gives the fallback");
            ModAssert.CloseTo(9, Formula.Evaluate("2 +* 3", null, 9), what: "broken text gives the fallback");
        }

        [ModTest("formula/syntax_errors_are_reported")]
        static void SyntaxErrorsAreReported(ModTestContext ctx)
        {
            foreach (var broken in new[] { "2 +", "(1 + 2", "foo(1)", "max()", "rand(1)", "1 2", "a ? b", "#" })
            {
                ModAssert.False(Formula.TryParse(broken, out _, out var error), $"'{broken}' must not parse");
                ModAssert.NotNull(error, $"'{broken}' has a message");
            }
            ModAssert.True(Formula.TryParse("  max(1, a.atk) ", out var node, out _), "spaces around are fine");
            ModAssert.Equal(FormulaNodeKind.Call.ToString(), node.Kind.ToString(), "call node");
        }

        [ModTest("formula/variables_lists_reads_once")]
        static void VariablesListsReadsOnce(ModTestContext ctx)
        {
            var node = Formula.Parse("a.atk * 2 + max(level, a.atk) - b.def");
            var names = node.Variables();
            ModAssert.Equal(3, names.Count, "three distinct names");
            ModAssert.Equal("a.atk", names[0], "first use first");
            ModAssert.Equal("level", names[1], "then level");
            ModAssert.Equal("b.def", names[2], "then b.def");
        }
    }
}
