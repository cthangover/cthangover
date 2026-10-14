using System;
using System.Collections.Generic;

namespace Cthangover.Core.Scenes.Parsing
{
    /// <summary>
    /// Read-only view of upcoming tokens used by <see cref="ILeafParser.CanParse"/>
    /// and <see cref="ILeafParser.Parse"/>. Wraps a <see cref="ConditionParser.Token"/>
    /// list and position index. Peek methods do not advance — only
    /// <see cref="Advance"/> consumes.
    ///
    /// Also carries the <paramref name="parseSubExpr"/> callback injected by
    /// <see cref="ExpressionParser"/> so that leaf parsers can recursively
    /// parse sub-expressions (e.g. inside parentheses).
    /// </summary>
    public class TokenPeeker
    {
        private readonly List<ConditionParser.Token> _tokens;
        private int _pos;
        private readonly Func<ICondition> _parseSubExpr;

        /// <summary>Creates a peeker over a token list.</summary>
        /// <param name="tokens">Full token list produced by <see cref="ConditionParser"/>.</param>
        /// <param name="parseSubExpr">Callback for recursively parsing parenthesised sub-expressions; may be null for CanParse-only use.</param>
        public TokenPeeker(List<ConditionParser.Token> tokens, Func<ICondition> parseSubExpr)
        {
            _tokens = tokens;
            _pos = 0;
            _parseSubExpr = parseSubExpr;
        }

        /// <summary>The token at the current position (EOF token when the list is exhausted).</summary>
        public ConditionParser.Token Peek => _tokens[_pos];
        /// <summary>True when the position moved past the last token.</summary>
        public bool AtEnd => _pos >= _tokens.Count;

        /// <summary>Returns the token <paramref name="offset"/> positions ahead without consuming; clamps to the EOF token past the end.</summary>
        public ConditionParser.Token PeekAhead(int offset)
        {
            var idx = _pos + offset;
            return idx < _tokens.Count ? _tokens[idx] : _tokens[_tokens.Count - 1];
        }

        /// <summary>True when the current token has the expected type (never matches at end of input).</summary>
        public bool Check(ConditionParser.TokenType type) => !AtEnd && Peek.Type == type;

        /// <summary>Consumes and returns the current token, moving the position forward.</summary>
        public ConditionParser.Token Advance()
        {
            var token = Peek;
            _pos++;
            return token;
        }

        /// <summary>Consumes the current token when it matches, otherwise throws with a parser error message.</summary>
        public ConditionParser.Token Expect(ConditionParser.TokenType type, string errorMsg)
        {
            if (AtEnd || Peek.Type != type)
                throw new FormatException($"Condition parse error: {errorMsg}, got '{(AtEnd ? "EOF" : Peek.Value)}'");
            return Advance();
        }

        /// <summary>Clones the position for speculative CanParse checks.</summary>
        public TokenPeeker Snapshot()
        {
            var clone = new TokenPeeker(_tokens, _parseSubExpr);
            clone._pos = _pos;
            return clone;
        }

        /// <summary>Parses a sub-expression using the injected callback.</summary>
        public ICondition ParseSubExpr() => _parseSubExpr();
    }
}
