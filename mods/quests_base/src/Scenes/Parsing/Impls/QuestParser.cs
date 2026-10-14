using System;

namespace Cthangover.Core.Scenes.Parsing.Impls
{
    /// <summary>
    /// Parses quest expressions: <c>QuestId.method("arg")</c> and
    /// <c>QuestId.Property comparison</c>.
    ///
    /// Matches when the stream contains an identifier followed by a dot
    /// that is NOT <c>true.</c> or <c>false.</c> (those are quest IDs,
    /// not boolean literals followed by quest access — handled here because
    /// <see cref="BoolLiteralParser"/> already rejected them).
    /// </summary>
    public class QuestParser : ILeafParser
    {
        /// <summary>True for identifiers followed by a dot (dot-forms that survived the boolean-literal check are quest references).</summary>
        public bool CanParse(TokenPeeker peeker)
        {
            if (peeker.AtEnd || peeker.Peek.Type != ConditionParser.TokenType.Ident)
                return false;

            var next = peeker.PeekAhead(1);
            return next.Type == ConditionParser.TokenType.Dot;
        }

        /// <summary>Dispatches on the member after the dot: <c>hasTag</c>/<c>notHasTag</c>, <c>IsActive</c>, or <c>Status</c>/<c>DataStatus</c> comparisons; unknown members throw.</summary>
        public ICondition Parse(TokenPeeker peeker, Func<ICondition> parseSubExpr)
        {
            var questId = peeker.Advance().Value;
            peeker.Expect(ConditionParser.TokenType.Dot, "expected '.' after quest id");

            var methodToken = peeker.Expect(ConditionParser.TokenType.Ident, "expected method or property name after '.'");
            var method = methodToken.Value;

            if (method == "hasTag" || method == "notHasTag")
            {
                peeker.Expect(ConditionParser.TokenType.LParen, "expected '('");
                var tag = peeker.Expect(ConditionParser.TokenType.String, "expected string argument");
                peeker.Expect(ConditionParser.TokenType.RParen, "expected ')'");
                return new QuestMethodCondition(questId, method, tag.Value);
            }

            if (method == "IsActive")
            {
                return new QuestBoolPropCondition(questId, method);
            }

            if (method == "Status" || method == "DataStatus")
            {
                var op = ParseCompOp(peeker);
                var num = peeker.Expect(ConditionParser.TokenType.Number, "expected number after comparison");
                return new QuestPropCondition(questId, method, op, int.Parse(num.Value));
            }

            throw new FormatException($"Condition parse error: unknown quest property/method '{method}'");
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