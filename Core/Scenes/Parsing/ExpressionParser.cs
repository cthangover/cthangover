using System;
using System.Collections.Generic;
using Cthangover.Core.Scenes.Parsing.Impls;

namespace Cthangover.Core.Scenes.Parsing
{
    /// <summary>
    /// Pratt precedence-climbing parser that converts a token list into an
    /// <see cref="ICondition"/> tree. Operators are resolved through
    /// <see cref="OperatorRegistry"/>; leaf values through
    /// <see cref="LeafParserRegistry"/>.
    ///
    /// Precedence values: higher = tighter binding. Infix operators use
    /// <c>op.Precedence</c> for the right operand to enforce left-
    /// associativity. The loop condition is strict greater-than
    /// (<c>></c>) so that operators at the current precedence level
    /// are consumed from left to right.
    ///
    /// The parser makes a single left-to-right pass over the token list,
    /// consuming each token exactly once. No backtracking, no regex.
    /// </summary>
    public class ExpressionParser
    {
        private TokenPeeker _peeker;

        static ExpressionParser()
        {
            OperatorRegistry.Register(new AndOpParser());
            OperatorRegistry.Register(new OrOpParser());
            OperatorRegistry.Register(new NotOpParser());
        }

        /// <summary>
        /// Parses a complete token list into an <see cref="ICondition"/> tree.
        /// </summary>
        public ICondition Parse(List<ConditionParser.Token> tokens)
        {
            _peeker = new TokenPeeker(tokens, null);
            var parseSubExpr = new Func<ICondition>(() => ParseExpr(0));
            _peeker = new TokenPeeker(tokens, parseSubExpr);

            var result = ParseExpr(0);

            if (!_peeker.AtEnd && _peeker.Peek.Type != ConditionParser.TokenType.EOF)
                throw new FormatException(
                    $"Condition parse error: unexpected token '{_peeker.Peek.Value}' at end of expression");

            return result;
        }

        private ICondition ParseExpr(int minPrecedence)
        {
            var left = ParsePrefix();

            while (!_peeker.AtEnd)
            {
                var op = OperatorRegistry.Get(_peeker.Peek.Type);
                if (op == null || !op.IsInfix || op.Precedence <= minPrecedence)
                    break;

                _peeker.Advance();
                var right = ParseExpr(op.Precedence);
                left = op.BuildInfix(left, right);
            }

            return left;
        }

        private ICondition ParsePrefix()
        {
            if (_peeker.Check(ConditionParser.TokenType.LParen))
            {
                _peeker.Advance();
                var expr = ParseExpr(0);
                _peeker.Expect(ConditionParser.TokenType.RParen, "expected ')' after expression");
                return expr;
            }

            var op = _peeker.AtEnd ? null : OperatorRegistry.Get(_peeker.Peek.Type);
            if (op != null && op.IsPrefix)
            {
                _peeker.Advance();
                var operand = ParseExpr(op.Precedence);
                return op.BuildPrefix(operand);
            }

            var leafParser = LeafParserRegistry.FindParser(_peeker);
            if (leafParser == null)
                throw new FormatException(
                    $"Condition parse error: unexpected token '{(_peeker.AtEnd ? "EOF" : _peeker.Peek.Value)}'");

            return leafParser.Parse(_peeker, () => ParseExpr(0));
        }
    }
}
