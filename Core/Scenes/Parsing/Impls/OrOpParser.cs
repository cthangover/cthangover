using Cthangover.Core.Scenes.Nodes.Impls;

namespace Cthangover.Core.Scenes.Parsing.Impls
{
    /// <summary>
    /// Pratt parser binding for the <c>||</c> token.
    /// Binds looser than <c>&amp;&amp;</c> (precedence 40 vs 30).
    /// </summary>
    public class OrOpParser : IOperatorParser
    {
        public ConditionParser.TokenType Token => ConditionParser.TokenType.Or;
        public int Precedence => 10;
        public bool IsPrefix => false;
        public bool IsInfix => true;

        public ICondition BuildInfix(ICondition left, ICondition right) =>
            new OrNode((ConditionNode)left, (ConditionNode)right);

        public ICondition BuildPrefix(ICondition operand) => null;
    }
}
