using Cthangover.Core.Scenes.Nodes.Impls;

namespace Cthangover.Core.Scenes.Parsing.Impls
{
    /// <summary>
    /// Pratt parser binding for the <c>!</c> token — unary negation.
    /// Highest precedence (50) to bind tightly to the next expression.
    /// Right-associative: <c>!!A</c> parses as <c>!( !(A) )</c>.
    /// </summary>
    public class NotOpParser : IOperatorParser
    {
        public ConditionParser.TokenType Token => ConditionParser.TokenType.Not;
        public int Precedence => 50;
        public bool IsPrefix => true;
        public bool IsInfix => false;

        public ICondition BuildInfix(ICondition left, ICondition right) => null;

        public ICondition BuildPrefix(ICondition operand) =>
            new NotNode((ConditionNode)operand);
    }
}
