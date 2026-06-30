using System;
using System.Collections.Generic;
using Cthangover.Core.Quests;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Evaluates condition expression strings by parsing them into an AST via
    /// <see cref="ConditionParser"/> and recursively walking the tree. Maintains a
    /// static dictionary of named flags settable via <see cref="SetFlag"/>, enabling
    /// scenario scripts to control branching through flag-based conditions. Quest
    /// conditions are resolved through <see cref="QuestFactory"/>.
    /// </summary>
    public static class ScenarioCondition
    {
        private static readonly Dictionary<string, string> flags = new();

        /// <summary>
        /// Sets a named flag value in the static flag dictionary. Used by scenario
        /// scripts to set conditions that can be checked in subsequent
        /// <see cref="Evaluate"/> calls.
        /// </summary>
        /// <param name="key">The flag name.</param>
        /// <param name="value">The flag value, which may be parsed as integer for numeric comparisons.</param>
        public static void SetFlag(string key, string value)
        {
            flags[key] = value;
        }

        /// <summary>
        /// Parses and evaluates a condition expression string. Returns <c>true</c> for
        /// null or whitespace-only input (vacuous truth). Returns <c>false</c> on parse
        /// errors. Supports boolean combinators (<c>&amp;&amp;</c>, <c>||</c>, <c>!</c>),
        /// numeric comparisons on flags and quest properties, and quest tag membership
        /// checks (<c>hasTag</c>, <c>notHasTag</c>).
        /// </summary>
        /// <param name="condition">The condition expression string to evaluate.</param>
        public static bool Evaluate(string condition)
        {
            if (string.IsNullOrWhiteSpace(condition))
                return true;

            ConditionNode ast;
            try
            {
                ast = ConditionParser.Parse(condition);
            }
            catch (FormatException ex)
            {
                GameLogger.Log("COND", $"parse error: {ex.Message}", LogLevel.Error);
                return false;
            }

            if (ast == null)
                return true;

            return EvaluateNode(ast);
        }

        private static bool EvaluateNode(ConditionNode node)
        {
            return node switch
            {
                BinaryExpr bin => bin.Op == "and"
                    ? EvaluateNode(bin.Left) && EvaluateNode(bin.Right)
                    : EvaluateNode(bin.Left) || EvaluateNode(bin.Right),

                UnaryExpr un => !EvaluateNode(un.Operand),

                QuestMethodCondition qm => EvaluateQuestMethod(qm),
                QuestPropCondition qp => EvaluateQuestProp(qp),
                FlagCondition fc => EvaluateFlag(fc),

                _ => true
            };
        }

        private static bool EvaluateQuestMethod(QuestMethodCondition qm)
        {
            try
            {
                var quest = QuestFactory.Instance.Get(qm.QuestId);
                return qm.Method == "hasTag"
                    ? quest.ContainsTag(qm.Tag)
                    : quest.NotContainsTag(qm.Tag);
            }
            catch (KeyNotFoundException)
            {
                GameLogger.Log("COND", $"quest '{qm.QuestId}' not found, condition → false", LogLevel.Error);
                return false;
            }
        }

        private static bool EvaluateQuestProp(QuestPropCondition qp)
        {
            try
            {
                var quest = QuestFactory.Instance.Get(qp.QuestId);
                var actual = qp.Property == "Status"
                    ? (int)quest.Status
                    : quest.Data.Status;
                return Compare(qp.Op, actual, qp.Value);
            }
            catch (KeyNotFoundException)
            {
                GameLogger.Log("COND", $"quest '{qp.QuestId}' not found, condition → false", LogLevel.Error);
                return false;
            }
        }

        private static bool EvaluateFlag(FlagCondition fc)
        {
            if (!flags.TryGetValue(fc.Key, out var current))
                return fc.Op == "!=";

            if (int.TryParse(current, out var ci) && int.TryParse(fc.Value, out var fi))
                return Compare(fc.Op, ci, fi);

            return fc.Op switch
            {
                "==" => current == fc.Value,
                "!=" => current != fc.Value,
                _ => false
            };
        }

        private static bool Compare(string op, int a, int b)
        {
            return op switch
            {
                "==" => a == b,
                "!=" => a != b,
                ">=" => a >= b,
                "<=" => a <= b,
                ">" => a > b,
                "<" => a < b,
                _ => false
            };
        }
    }
}
