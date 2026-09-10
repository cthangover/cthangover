namespace Cthangover.Core.Scenes;

public interface ICondition
{
    /// <summary>
    /// Evaluates this node against the given runtime context and returns
    /// the boolean result. Called recursively — for combinator nodes like
    /// <see cref="AndNode"/> this triggers a depth-first walk.
    /// </summary>
    /// <param name="ctx">Runtime state: flag dictionary and quest factory.</param>
    bool Evaluate(EvaluationContext ctx);
}