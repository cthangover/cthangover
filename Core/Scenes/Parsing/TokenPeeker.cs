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

        public TokenPeeker(List<ConditionParser.Token> tokens, Func<ICondition> parseSubExpr)
        {
            _tokens = tokens;
            _pos = 0;
            _parseSubExpr = parseSubExpr;
        }

        public ConditionParser.Token Peek => _tokens[_pos];
        public bool AtEnd => _pos >= _tokens.Count;

        public ConditionParser.Token PeekAhead(int offset)
        {
            var idx = _pos + offset;
            return idx < _tokens.Count ? _tokens[idx] : _tokens[_tokens.Count - 1];
        }

        public bool Check(ConditionParser.TokenType type) => !AtEnd && Peek.Type == type;

        public ConditionParser.Token Advance()
        {
            var token = Peek;
            _pos++;
            return token;
        }

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
