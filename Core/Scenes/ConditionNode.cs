namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Abstract base for all nodes in the condition expression AST produced by
    /// <see cref="ConditionParser"/>. Every node implements
    /// <see cref="Evaluate(EvaluationContext)"/> — the tree is walked polymorphically
    /// rather than via a central switch statement.
    ///
    /// This design means that a mod assembly can define a custom
    /// <see cref="ConditionNode"/> subclass, and any code that constructs an AST
    /// containing it (including the core parser once extended) automatically
    /// evaluates it — no registration, no dispatch table, no <c>_ => true</c> fallthrough.
    ///
    /// Built-in node types:
    /// <list type="bullet">
    /// <item><see cref="AndNode"/> / <see cref="OrNode"/> — binary boolean combinators with short-circuit</item>
    /// <item><see cref="NotNode"/> — unary negation</item>
    /// <item><see cref="BoolLiteral"/> — constant <c>true</c> / <c>false</c></item>
    /// <item><see cref="FlagCondition"/> — runtime flag comparison</item>
    /// <item><see cref="QuestMethodCondition"/> — quest tag membership</item>
    /// <item><see cref="QuestPropCondition"/> — quest int-property comparison</item>
    /// <item><see cref="QuestBoolPropCondition"/> — quest boolean property</item>
    /// </list>
    /// </summary>
    public abstract class ConditionNode : ICondition
    {
        /// <summary>
        /// Evaluates this node against the given runtime context and returns
        /// the boolean result. Called recursively — for combinator nodes like
        /// <see cref="AndNode"/> this triggers a depth-first walk.
        /// </summary>
        /// <param name="ctx">Runtime state: flag dictionary and quest factory.</param>
        public abstract bool Evaluate(EvaluationContext ctx);
    }
}
