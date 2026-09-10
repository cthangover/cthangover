namespace Cthangover.Core.Scenes.Nodes.Impls
{
    /// <summary>
    /// AST node for boolean NOT — negates the result of <see cref="Operand"/>.
    /// Produced by <see cref="ConditionParser"/> from the <c>!</c> token.
    /// A double <c>!!</c> produces a <see cref="NotNode"/> wrapping another
    /// <see cref="NotNode"/> — the parser does not fold constants.
    /// </summary>
    public class NotNode : ConditionNode
    {
        /// <summary>The expression whose boolean result is negated.</summary>
        public ICondition Operand { get; }

        public NotNode(ICondition operand)
        {
            Operand = operand;
        }

        public override bool Evaluate(EvaluationContext ctx) =>
            !Operand.Evaluate(ctx);
    }
}
