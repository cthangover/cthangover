using System;

namespace Cthangover.Core.Scenes.Parsing.Impls
{
    /// <summary>
    /// Parses parenthesised sub-expressions: <c>( expr )</c>.
    /// This parser is registered last in
    /// <see cref="LeafParserRegistry"/> because <c>(</c> is unambiguous
    /// — no other leaf parser starts with that token. Still placed at the
    /// end so that all identifier-based parsers get a chance first.
    /// </summary>
    public class GroupParser : ILeafParser
    {
        /// <summary>True at any opening parenthesis — unambiguous, so this parser is registered last.</summary>
        public bool CanParse(TokenPeeker peeker)
        {
            return !peeker.AtEnd && peeker.Peek.Type == ConditionParser.TokenType.LParen;
        }

        /// <summary>Parses the parenthesised sub-expression recursively and returns its subtree unchanged.</summary>
        public ICondition Parse(TokenPeeker peeker, Func<ICondition> parseSubExpr)
        {
            peeker.Advance();
            var expr = parseSubExpr();
            peeker.Expect(ConditionParser.TokenType.RParen, "expected ')' after expression");
            return expr;
        }
    }
}
