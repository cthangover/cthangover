namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// An AST leaf node representing a comparison between a named runtime flag and a
    /// literal value. Evaluated by <see cref="ScenarioCondition.EvaluateFlag"/> against
    /// the static flag dictionary managed by <see cref="ScenarioCondition.SetFlag"/>.
    /// Supports both numeric and string comparisons; if both the stored and expected
    /// values parse as integers, a numeric comparison is performed.
    /// </summary>
    public class FlagCondition : ConditionNode
    {
        /// <summary>The flag key to look up in the runtime flag dictionary.</summary>
        public string Key { get; }

        /// <summary>The comparison operator (<c>==</c>, <c>!=</c>, etc.).</summary>
        public string Op { get; }

        /// <summary>The literal value to compare the flag against.</summary>
        public string Value { get; }

        /// <summary>
        /// Creates a flag comparison condition node.
        /// </summary>
        /// <param name="key">The flag key.</param>
        /// <param name="op">The comparison operator.</param>
        /// <param name="value">The literal value to compare against.</param>
        public FlagCondition(string key, string op, string value)
        {
            Key = key;
            Op = op;
            Value = value;
        }
    }
}
