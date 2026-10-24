namespace Cthangover.Mods.Stats
{
    using System;
    using Cthangover.Core.Scenes;

    /// <summary>
    /// <c>stat(who, stat) &gt;= n</c> and <c>level(who) &gt;= n</c> in any condition: a
    /// scenario header, a map event, an interactive, an option gate. Values compare as
    /// whole numbers, rounded like battles and checks round them.
    /// </summary>
    public class StatCompareCondition : ConditionNode
    {
        /// <summary>Creates the condition; a null <paramref name="stat"/> compares the level.</summary>
        public StatCompareCondition(string who, string stat, string op, int value)
        {
            Who = who;
            Stat = stat;
            Op = op;
            Value = value;
        }

        /// <summary>Whose stat (a character id or <c>player</c>).</summary>
        public string Who { get; }

        /// <summary>The stat id; null for the level.</summary>
        public string Stat { get; }

        /// <summary>The comparison operator.</summary>
        public string Op { get; }

        /// <summary>The number compared with.</summary>
        public int Value { get; }

        /// <inheritdoc />
        public override bool Evaluate(EvaluationContext ctx)
        {
            var current = Stat == null
                ? StatsService.LevelOf(Who)
                : (int)Math.Round(StatsService.Sheet(Who).Get(Stat), MidpointRounding.AwayFromZero);
            return ConditionCompare.Compare(Op, current, Value);
        }
    }
}
