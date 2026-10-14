namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// An AST leaf node representing a comparison between a named runtime flag and a
    /// literal value. Looks up the flag key in the <see cref="EvaluationContext.Flags"/>
    /// dictionary at evaluation time. Supports both numeric and string comparisons;
    /// if both the stored and expected values parse as integers, a numeric comparison
    /// is performed via <see cref="ConditionCompare.Compare"/>.
    ///
    /// When the flag is absent from the dictionary, the condition returns <c>true</c>
    /// for <c>!=</c> and <c>false</c> for all other operators — this "missing means
    /// not-equal" semantics allows scenario scripts to gate on flags that may not
    /// yet have been set.
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

        /// <summary>Reads the live flag value and compares it; a missing flag counts as "not equal".</summary>
        public override bool Evaluate(EvaluationContext ctx)
        {
            if (!ctx.Flags.TryGetValue(Key, out var current))
                return Op == "!=";

            if (int.TryParse(current, out var ci) && int.TryParse(Value, out var fi))
                return ConditionCompare.Compare(Op, ci, fi);

            return Op switch
            {
                "==" => current == Value,
                "!=" => current != Value,
                _ => false
            };
        }
    }
}
