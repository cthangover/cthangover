using Cthangover.Core.Scenes.Nodes.Impls;

namespace Cthangover.Core.Scenes.Parsing.Impls
{
    /// <summary>
    /// Pratt parser binding for the <c>&amp;&amp;</c> token.
    /// Binds tighter than <c>||</c> (precedence 30 vs 40), so
    /// <c>A || B && C</c> parses as <c>||(A, &&(B, C))</c>.
    /// </summary>
    public class AndOpParser : IOperatorParser
    {
        public ConditionParser.TokenType Token => ConditionParser.TokenType.And;
        public int Precedence => 20;
        public bool IsPrefix => false;
        public bool IsInfix => true;

        public ICondition BuildInfix(ICondition left, ICondition right) =>
            new AndNode((ConditionNode)left, (ConditionNode)right);

        public ICondition BuildPrefix(ICondition operand) => null;
    }
}
