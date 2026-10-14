using Cthangover.Core.Scenes.Nodes.Impls;

namespace Cthangover.Core.Scenes.Parsing.Impls
{
    /// <summary>
    /// Pratt parser binding for the <c>||</c> token.
    /// Binds looser than <c>&amp;&amp;</c> (precedence 40 vs 30).
    /// </summary>
    public class OrOpParser : IOperatorParser
    {
        /// <summary>Binds the <c>||</c> token type.</summary>
        public ConditionParser.TokenType Token => ConditionParser.TokenType.Or;
        /// <summary>Precedence 10 — loosest, so <c>&amp;&amp;</c> groups tighter inside <c>||</c> operands.</summary>
        public int Precedence => 10;
        /// <summary><c>||</c> is strictly an infix operator.</summary>
        public bool IsPrefix => false;
        /// <summary><c>||</c> is strictly an infix operator.</summary>
        public bool IsInfix => true;

        /// <summary>Builds an <see cref="OrNode"/> with left-associative operands.</summary>
        public ICondition BuildInfix(ICondition left, ICondition right) =>
            new OrNode((ConditionNode)left, (ConditionNode)right);

        /// <summary>Never called — <see cref="IsPrefix"/> is false.</summary>
        public ICondition BuildPrefix(ICondition operand) => null;
    }
}
