namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Abstract base class for all nodes in the condition expression abstract syntax
    /// tree produced by <see cref="ConditionParser"/>. The tree is walked by
    /// <see cref="ScenarioCondition.EvaluateNode"/> at runtime. Subclasses include
    /// boolean combinators (<see cref="BinaryExpr"/>, <see cref="UnaryExpr"/>), flag
    /// comparisons (<see cref="FlagCondition"/>), quest property comparisons
    /// (<see cref="QuestPropCondition"/>), and quest method calls
    /// (<see cref="QuestMethodCondition"/>).
    /// </summary>
    public abstract class ConditionNode
    {
    }
}
