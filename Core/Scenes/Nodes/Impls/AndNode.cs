namespace Cthangover.Core.Scenes.Nodes.Impls
{
    /// <summary>
    /// AST node for boolean AND — evaluates <see cref="Left"/> first, then
    /// <see cref="Right"/> only if <see cref="Left"/> is <c>true</c>.
    /// This short-circuit behaviour is inherited from the C# <c>&amp;&amp;</c>
    /// operator and requires no explicit control flow.
    ///
    /// Produced by <see cref="ConditionParser"/> from the <c>&amp;&amp;</c>
    /// token in condition expressions. When a mod constructs an AST manually,
    /// it wraps <see cref="Left"/> and <see cref="Right"/> directly without
    /// involving the parser.
    /// </summary>
    public class AndNode : ConditionNode
    {
        /// <summary>Evaluated first; if <c>false</c>, <see cref="Right"/> is skipped.</summary>
        public ICondition Left { get; }

        /// <summary>Evaluated only when <see cref="Left"/> returns <c>true</c>.</summary>
        public ICondition Right { get; }

        public AndNode(ICondition left, ICondition right)
        {
            Left = left;
            Right = right;
        }

        public override bool Evaluate(EvaluationContext ctx) =>
            Left.Evaluate(ctx) && Right.Evaluate(ctx);
    }
}
