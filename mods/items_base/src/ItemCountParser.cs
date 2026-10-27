namespace Cthangover.Mods.Items
{
    using System;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Scenes.Parsing;

    /// <summary>Adds item('id') OP count to the shared condition language, usable by quests, choices and map pages alike.</summary>
    public class ItemCountParser : ILeafParser
    {
        /// <inheritdoc />
        public bool CanParse(TokenPeeker peeker) => !peeker.AtEnd
            && peeker.Peek.Type == ConditionParser.TokenType.Ident && peeker.Peek.Value == "item"
            && peeker.PeekAhead(1).Type == ConditionParser.TokenType.LParen;

        /// <inheritdoc />
        public ICondition Parse(TokenPeeker peeker, Func<ICondition> parseSubExpr)
        {
            peeker.Advance();
            peeker.Expect(ConditionParser.TokenType.LParen, "expected '(' after item");
            if (!peeker.Check(ConditionParser.TokenType.String) && !peeker.Check(ConditionParser.TokenType.Ident))
                throw new FormatException("item() expects an item id");
            var id = peeker.Advance().Value;
            peeker.Expect(ConditionParser.TokenType.RParen, "expected ')' after item id");
            var op = peeker.Peek.Type switch
            {
                ConditionParser.TokenType.Eq => "==", ConditionParser.TokenType.Neq => "!=",
                ConditionParser.TokenType.Ge => ">=", ConditionParser.TokenType.Le => "<=",
                ConditionParser.TokenType.Gt => ">", ConditionParser.TokenType.Lt => "<",
                _ => throw new FormatException("item() expects a comparison")
            };
            peeker.Advance();
            var count = peeker.Expect(ConditionParser.TokenType.Number, "item() expects a whole-number count");
            return new ItemCountCondition(id, op, int.Parse(count.Value));
        }
    }
}
