namespace Cthangover.Core.Scenes.Nodes.Impls
{
    /// <summary>
    /// AST leaf node representing a constant boolean value.
    /// Produced by <see cref="ConditionParser"/> from the <c>true</c> and
    /// <c>false</c> keywords in condition expressions. When an identifier
    /// <c>true</c> or <c>false</c> is immediately followed by a dot
    /// (e.g. <c>true.IsActive</c>), the parser treats it as a quest ID
    /// rather than a boolean literal, so quests named "true" or "false"
    /// are still accessible.
    /// </summary>
    public class BoolLiteral : ConditionNode
    {
        /// <summary>The constant boolean value: <c>true</c> or <c>false</c>.</summary>
        public bool Value;

        /// <summary>Creates a constant node from the parsed keyword.</summary>
        /// <param name="value">Constant result of the condition.</param>
        public BoolLiteral(bool value) => Value = value;

        /// <summary>Returns the constant regardless of context.</summary>
        public override bool Evaluate(EvaluationContext ctx) => Value;
    }
}
