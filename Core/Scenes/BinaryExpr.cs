namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// An AST node representing a binary boolean operation (<c>"and"</c> or <c>"or"</c>)
    /// between two child <see cref="ConditionNode"/> instances. Produced by
    /// <see cref="ConditionParser"/> during parsing of <c>&amp;&amp;</c> and <c>||</c>
    /// operators. Evaluated by <see cref="ScenarioCondition.EvaluateNode"/> using
    /// short-circuit logic.
    /// </summary>
    public class BinaryExpr : ConditionNode
    {
        /// <summary>The left-hand operand of the boolean expression.</summary>
        public ConditionNode Left { get; }

        /// <summary>The operator: <c>"and"</c> for conjunction, <c>"or"</c> for disjunction.</summary>
        public string Op { get; }

        /// <summary>The right-hand operand of the boolean expression.</summary>
        public ConditionNode Right { get; }

        /// <summary>
        /// Creates a binary boolean expression node.
        /// </summary>
        /// <param name="left">The left operand <see cref="ConditionNode"/>.</param>
        /// <param name="op">The operator string (<c>"and"</c> or <c>"or"</c>).</param>
        /// <param name="right">The right operand <see cref="ConditionNode"/>.</param>
        public BinaryExpr(ConditionNode left, string op, ConditionNode right)
        {
            Left = left;
            Op = op;
            Right = right;
        }
    }
}
