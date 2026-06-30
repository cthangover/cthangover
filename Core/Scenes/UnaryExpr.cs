namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// An AST node representing a unary boolean negation (<c>"not"</c>) applied to a
    /// single child <see cref="ConditionNode"/>. Produced by <see cref="ConditionParser"/>
    /// during parsing of the <c>!</c> operator. Evaluated by
    /// <see cref="ScenarioCondition.EvaluateNode"/>.
    /// </summary>
    public class UnaryExpr : ConditionNode
    {
        /// <summary>The operator, always <c>"not"</c>.</summary>
        public string Op { get; }

        /// <summary>The operand <see cref="ConditionNode"/> to negate.</summary>
        public ConditionNode Operand { get; }

        /// <summary>
        /// Creates a unary negation expression node.
        /// </summary>
        /// <param name="op">The operator string (<c>"not"</c>).</param>
        /// <param name="operand">The operand <see cref="ConditionNode"/> to negate.</param>
        public UnaryExpr(string op, ConditionNode operand)
        {
            Op = op;
            Operand = operand;
        }
    }
}
