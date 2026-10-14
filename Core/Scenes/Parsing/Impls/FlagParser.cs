using System;

namespace Cthangover.Core.Scenes.Parsing.Impls
{
    /// <summary>
    /// Parses flag comparison expressions: <c>flag_name OP literal</c>.
    /// Matches when the stream contains an identifier followed by a
    /// comparison operator (== != >= <= > <).
    ///
    /// Must be tried AFTER <see cref="QuestParser"/> and
    /// <see cref="BoolLiteralParser"/> in the registry — those parsers
    /// also start with an identifier but consume the dot or check for
    /// true/false respectively. By the time <see cref="CanParse"/> is
    /// called here, any dot-followed or keyword identifiers have already
    /// been claimed.
    /// </summary>
    public class FlagParser : ILeafParser
    {
        /// <summary>True for identifiers immediately followed by a comparison operator (dot-forms are claimed by earlier parsers).</summary>
        public bool CanParse(TokenPeeker peeker)
        {
            if (peeker.AtEnd || peeker.Peek.Type != ConditionParser.TokenType.Ident)
                return false;

            var next = peeker.PeekAhead(1);
            return next.Type == ConditionParser.TokenType.Eq
                || next.Type == ConditionParser.TokenType.Neq
                || next.Type == ConditionParser.TokenType.Ge
                || next.Type == ConditionParser.TokenType.Le
                || next.Type == ConditionParser.TokenType.Gt
                || next.Type == ConditionParser.TokenType.Lt;
        }

        /// <summary>Consumes <c>key OP literal</c> (string or number literal) and builds a <see cref="FlagCondition"/>.</summary>
        public ICondition Parse(TokenPeeker peeker, Func<ICondition> parseSubExpr)
        {
            var key = peeker.Advance().Value;
            var op = ParseCompOp(peeker);

            ConditionParser.Token valToken;
            if (peeker.Check(ConditionParser.TokenType.String) || peeker.Check(ConditionParser.TokenType.Number))
                valToken = peeker.Advance();
            else
                throw new FormatException($"Condition parse error: expected string or number after operator, got '{peeker.Peek.Value}'");

            return new FlagCondition(key, op, valToken.Value);
        }

        private static string ParseCompOp(TokenPeeker peeker)
        {
            if (peeker.Check(ConditionParser.TokenType.Eq)) { peeker.Advance(); return "=="; }
            if (peeker.Check(ConditionParser.TokenType.Neq)) { peeker.Advance(); return "!="; }
            if (peeker.Check(ConditionParser.TokenType.Ge)) { peeker.Advance(); return ">="; }
            if (peeker.Check(ConditionParser.TokenType.Le)) { peeker.Advance(); return "<="; }
            if (peeker.Check(ConditionParser.TokenType.Gt)) { peeker.Advance(); return ">"; }
            if (peeker.Check(ConditionParser.TokenType.Lt)) { peeker.Advance(); return "<"; }
            throw new FormatException($"Condition parse error: expected comparison operator, got '{peeker.Peek.Value}'");
        }
    }
}
