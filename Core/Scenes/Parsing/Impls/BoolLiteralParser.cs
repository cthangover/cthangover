using System;
using Cthangover.Core.Scenes.Nodes.Impls;

namespace Cthangover.Core.Scenes.Parsing.Impls
{
    /// <summary>
    /// Parses <c>true</c> and <c>false</c> keyword tokens into
    /// <see cref="BoolLiteral"/> nodes. Only matches when the
    /// keyword is not immediately followed by a dot — <c>true.IsActive</c>
    /// is NOT a boolean literal; it is a quest reference (handled by
    /// <see cref="QuestParser"/>).
    /// </summary>
    public class BoolLiteralParser : ILeafParser
    {
        public bool CanParse(TokenPeeker peeker)
        {
            if (peeker.AtEnd || peeker.Peek.Type != ConditionParser.TokenType.Ident)
                return false;

            var value = peeker.Peek.Value;
            if (value != "true" && value != "false")
                return false;

            var next = peeker.PeekAhead(1);
            return next.Type != ConditionParser.TokenType.Dot;
        }

        public ICondition Parse(TokenPeeker peeker, Func<ICondition> parseSubExpr)
        {
            var token = peeker.Advance();
            return new BoolLiteral(token.Value == "true");
        }
    }
}
