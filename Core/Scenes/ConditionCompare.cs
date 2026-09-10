namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Shared integer comparison utility consumed by <see cref="FlagCondition"/>
    /// and <see cref="QuestPropCondition"/> during evaluation. Both node types
    /// support the same six operators (<c>== != &gt;= &lt;= &gt; &lt;</c>) on
    /// integer values, so the comparison logic lives here to avoid duplication.
    ///
    /// Extracted from the former <c>ScenarioCondition.Compare</c> private method
    /// when evaluation moved into polymorphic <see cref="ConditionNode.Evaluate"/>
    /// methods.
    /// </summary>
    public static class ConditionCompare
    {
        /// <summary>
        /// Compares two integers using the given operator string.
        /// Unknown operators return <c>false</c>.
        /// </summary>
        public static bool Compare(string op, int a, int b)
        {
            return op switch
            {
                "==" => a == b,
                "!=" => a != b,
                ">=" => a >= b,
                "<=" => a <= b,
                ">" => a > b,
                "<" => a < b,
                _ => false
            };
        }
    }
}
