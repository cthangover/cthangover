using System;
using Cthangover.Core.Scenes;
using Cthangover.Core.Scenes.Nodes.Impls;
using Cthangover.Core.Scenes.Parsing;

namespace Engine.Tests.Scenes
{
    /// <summary>A leaf parser of the tests: claims <c>name()</c> and evaluates to true.</summary>
    public class ProbeLeafParser : ILeafParser
    {
        public ProbeLeafParser(string name)
        {
            Name = name;
        }

        public string Name { get; }

        public bool CanParse(TokenPeeker peeker)
            => !peeker.AtEnd && peeker.Peek.Type == ConditionParser.TokenType.Ident && peeker.Peek.Value == Name
               && peeker.PeekAhead(1).Type == ConditionParser.TokenType.LParen;

        public ICondition Parse(TokenPeeker peeker, Func<ICondition> parseSubExpr)
        {
            peeker.Advance();
            peeker.Expect(ConditionParser.TokenType.LParen, "expected '('");
            peeker.Expect(ConditionParser.TokenType.RParen, "expected ')'");
            return new BoolLiteral(true);
        }
    }
}
