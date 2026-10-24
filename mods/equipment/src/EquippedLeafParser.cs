namespace Cthangover.Mods.Equipment
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Scenes.Parsing;

    /// <summary>
    /// <c>equipped(who, item)</c> or <c>equipped(item)</c> (the player) in any condition: a
    /// door that opens for the one carrying the old key, a guard who salutes the uniform.
    /// Ids with a slash are quoted: <c>equipped('rpg_plus/iron_sword')</c>.
    /// </summary>
    public class EquippedLeafParser : ILeafParser
    {
        /// <inheritdoc />
        public bool CanParse(TokenPeeker peeker)
            => !peeker.AtEnd && peeker.Peek.Type == ConditionParser.TokenType.Ident && peeker.Peek.Value == "equipped"
               && peeker.PeekAhead(1).Type == ConditionParser.TokenType.LParen;

        /// <inheritdoc />
        public ICondition Parse(TokenPeeker peeker, Func<ICondition> parseSubExpr)
        {
            peeker.Advance();
            peeker.Expect(ConditionParser.TokenType.LParen, "expected '('");
            var args = new List<string>();
            while (peeker.Check(ConditionParser.TokenType.Ident) || peeker.Check(ConditionParser.TokenType.String))
            {
                args.Add(peeker.Advance().Value);
                if (!peeker.Check(ConditionParser.TokenType.Comma))
                    break;
                peeker.Advance();
            }
            peeker.Expect(ConditionParser.TokenType.RParen, "expected ')' after equipped(who, item)");
            if (args.Count is < 1 or > 2)
                throw new FormatException("Condition parse error: equipped(who, item) takes one or two arguments");
            return new EquippedCondition(args.Count == 2 ? args[0] : null, args[^1]);
        }
    }
}
