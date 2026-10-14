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
        /// <summary>Binds the <c>&amp;&amp;</c> token type.</summary>
        public ConditionParser.TokenType Token => ConditionParser.TokenType.And;
        /// <summary>Precedence 20 — tighter than <c>||</c> (10), looser than prefix <c>!</c> (50).</summary>
        public int Precedence => 20;
        /// <summary><c>&amp;&amp;</c> is strictly an infix operator.</summary>
        public bool IsPrefix => false;
        /// <summary><c>&amp;&amp;</c> is strictly an infix operator.</summary>
        public bool IsInfix => true;

        /// <summary>Builds an <see cref="AndNode"/> with left-associative operands.</summary>
        public ICondition BuildInfix(ICondition left, ICondition right) =>
            new AndNode((ConditionNode)left, (ConditionNode)right);

        /// <summary>Never called — <see cref="IsPrefix"/> is false.</summary>
        public ICondition BuildPrefix(ICondition operand) => null;
    }
}
