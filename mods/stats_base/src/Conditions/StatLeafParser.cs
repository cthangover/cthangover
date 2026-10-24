namespace Cthangover.Mods.Stats
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Scenes.Parsing;

    /// <summary>
    /// Adds the stats to the shared condition language:
    /// <list type="bullet">
    /// <item><c>stat(who, stat) OP n</c>, or <c>stat(stat) OP n</c> for the player;</item>
    /// <item><c>level(who) OP n</c>, or <c>level() OP n</c>;</item>
    /// <item><c>archetype(who, id)</c>, or <c>archetype(id)</c>.</item>
    /// </list>
    /// Arguments are names or quoted strings (ids with a slash must be quoted:
    /// <c>stat('village/slime', atk)</c>). The call form never collides with flags
    /// (<c>name OP value</c>) or quests (<c>quest.Property</c>): it claims only its three
    /// function names followed by <c>(</c>.
    /// </summary>
    public class StatLeafParser : ILeafParser
    {
        private static readonly string[] Functions = { "stat", "level", "archetype" };

        /// <inheritdoc />
        public bool CanParse(TokenPeeker peeker)
        {
            if (peeker.AtEnd || peeker.Peek.Type != ConditionParser.TokenType.Ident)
                return false;
            return Array.IndexOf(Functions, peeker.Peek.Value) >= 0
                   && peeker.PeekAhead(1).Type == ConditionParser.TokenType.LParen;
        }

        /// <inheritdoc />
        public ICondition Parse(TokenPeeker peeker, Func<ICondition> parseSubExpr)
        {
            var function = peeker.Advance().Value;
            var args = Arguments(peeker);
            switch (function)
            {
                case "stat":
                    if (args.Count is < 1 or > 2)
                        throw new FormatException("Condition parse error: stat(who, stat) takes one or two arguments");
                    var who = args.Count == 2 ? args[0] : null;
                    return Compare(peeker, who, args[^1]);
                case "level":
                    if (args.Count > 1)
                        throw new FormatException("Condition parse error: level(who) takes at most one argument");
                    return Compare(peeker, args.Count == 1 ? args[0] : null, null);
                default:
                    if (args.Count is < 1 or > 2)
                        throw new FormatException("Condition parse error: archetype(who, id) takes one or two arguments");
                    return new ArchetypeCondition(args.Count == 2 ? args[0] : null, args[^1]);
            }
        }

        private static List<string> Arguments(TokenPeeker peeker)
        {
            peeker.Expect(ConditionParser.TokenType.LParen, "expected '('");
            var args = new List<string>();
            if (peeker.Check(ConditionParser.TokenType.RParen))
            {
                peeker.Advance();
                return args;
            }
            while (true)
            {
                if (peeker.Check(ConditionParser.TokenType.Ident) || peeker.Check(ConditionParser.TokenType.String))
                    args.Add(peeker.Advance().Value);
                else
                    throw new FormatException($"Condition parse error: expected a name or a quoted id, got '{peeker.Peek.Value}'");
                if (peeker.Check(ConditionParser.TokenType.Comma))
                {
                    peeker.Advance();
                    continue;
                }
                peeker.Expect(ConditionParser.TokenType.RParen, "expected ')'");
                return args;
            }
        }

        private static StatCompareCondition Compare(TokenPeeker peeker, string who, string stat)
        {
            var op = CompareOp(peeker);
            var number = peeker.Expect(ConditionParser.TokenType.Number, "expected a number after the comparison");
            return new StatCompareCondition(who, stat, op, int.Parse(number.Value));
        }

        private static string CompareOp(TokenPeeker peeker)
        {
            var type = peeker.AtEnd ? ConditionParser.TokenType.EOF : peeker.Peek.Type;
            var op = type switch
            {
                ConditionParser.TokenType.Eq => "==",
                ConditionParser.TokenType.Neq => "!=",
                ConditionParser.TokenType.Ge => ">=",
                ConditionParser.TokenType.Le => "<=",
                ConditionParser.TokenType.Gt => ">",
                ConditionParser.TokenType.Lt => "<",
                _ => null
            };
            if (op == null)
                throw new FormatException($"Condition parse error: expected a comparison after stat()/level(), got '{(peeker.AtEnd ? "EOF" : peeker.Peek.Value)}'");
            peeker.Advance();
            return op;
        }
    }
}
