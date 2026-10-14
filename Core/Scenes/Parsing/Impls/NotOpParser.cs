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
        /// <summary>Binds the <c>!</c> token type.</summary>
        public ConditionParser.TokenType Token => ConditionParser.TokenType.Not;
        /// <summary>Precedence 50 — highest, so <c>!</c> binds to the immediately following expression.</summary>
        public int Precedence => 50;
        /// <summary><c>!</c> is strictly a prefix operator.</summary>
        public bool IsPrefix => true;
        /// <summary><c>!</c> is strictly a prefix operator.</summary>
        public bool IsInfix => false;

        /// <summary>Never called — <see cref="IsInfix"/> is false.</summary>
        public ICondition BuildInfix(ICondition left, ICondition right) => null;

        /// <summary>Builds a <see cref="NotNode"/> around the operand.</summary>
        public ICondition BuildPrefix(ICondition operand) =>
            new NotNode((ConditionNode)operand);
    }
}
