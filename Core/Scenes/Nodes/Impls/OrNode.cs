namespace Cthangover.Core.Scenes.Nodes.Impls
{
    /// <summary>
    /// AST node for boolean OR — evaluates <see cref="Left"/> first, then
    /// <see cref="Right"/> only if <see cref="Left"/> is <c>false</c>.
    /// This short-circuit behaviour is inherited from the C# <c>||</c>
    /// operator and requires no explicit control flow.
    ///
    /// Produced by <see cref="ConditionParser"/> from the <c>||</c>
    /// token in condition expressions.
    /// </summary>
    public class OrNode : ConditionNode
    {
        /// <summary>Evaluated first; if <c>true</c>, <see cref="Right"/> is skipped.</summary>
        public ICondition Left { get; }

        /// <summary>Evaluated only when <see cref="Left"/> returns <c>false</c>.</summary>
        public ICondition Right { get; }

        public OrNode(ICondition left, ICondition right)
        {
            Left = left;
            Right = right;
        }

        public override bool Evaluate(EvaluationContext ctx) =>
            Left.Evaluate(ctx) || Right.Evaluate(ctx);
    }
}
