using Cthangover.Core.Scenes;

namespace Engine.Tests.Scenarios
{
    /// <summary>
    /// Flag/boolean grammar of <see cref="ScenarioCondition"/>. The quest
    /// condition grammar (<c>QuestId.method(...)</c>, property comparisons)
    /// is owned by the quests_base engine mod — its QuestParser registers at
    /// mod load and is covered by the integration coverage test
    /// (coverage_quests), which runs with mods loaded.
    /// </summary>
    public class ScenarioConditionTests
    {
        [Fact]
        public void Evaluate_Null_ReturnsTrue()
        {
            Assert.True(ScenarioCondition.Evaluate(null));
        }

        [Fact]
        public void Evaluate_EmptyString_ReturnsTrue()
        {
            Assert.True(ScenarioCondition.Evaluate(""));
        }

        [Fact]
        public void Evaluate_Whitespace_ReturnsTrue()
        {
            Assert.True(ScenarioCondition.Evaluate("  \t "));
        }

        [Fact]
        public void Evaluate_FlagEqual_Match_ReturnsTrue()
        {
            ScenarioCondition.SetFlag("testFlag", "done");
            Assert.True(ScenarioCondition.Evaluate("testFlag == \"done\""));
        }

        [Fact]
        public void Evaluate_FlagEqual_NoMatch_ReturnsFalse()
        {
            ScenarioCondition.SetFlag("testFlag", "running");
            Assert.False(ScenarioCondition.Evaluate("testFlag == \"done\""));
        }

        [Fact]
        public void Evaluate_FlagNotEqual_Match_ReturnsTrue()
        {
            ScenarioCondition.SetFlag("testFlag", "running");
            Assert.True(ScenarioCondition.Evaluate("testFlag != \"done\""));
        }

        [Fact]
        public void Evaluate_FlagNotEqual_NoMatch_ReturnsFalse()
        {
            ScenarioCondition.SetFlag("testFlag", "done");
            Assert.False(ScenarioCondition.Evaluate("testFlag != \"done\""));
        }

        [Fact]
        public void Evaluate_FlagMissing_Equal_ReturnsFalse()
        {
            Assert.False(ScenarioCondition.Evaluate("missingFlag == \"x\""));
        }

        [Fact]
        public void Evaluate_FlagMissing_NotEqual_ReturnsTrue()
        {
            Assert.True(ScenarioCondition.Evaluate("missingFlag != \"x\""));
        }

        [Fact]
        public void Evaluate_FlagMissing_GreaterOrEqual_ReturnsFalse()
        {
            Assert.False(ScenarioCondition.Evaluate("missingFlag >= 0"));
        }

        [Fact]
        public void Evaluate_FlagNumeric_ComparisonWorks()
        {
            ScenarioCondition.SetFlag("count", "5");
            Assert.True(ScenarioCondition.Evaluate("count == 5"));
            Assert.True(ScenarioCondition.Evaluate("count >= 5"));
            Assert.True(ScenarioCondition.Evaluate("count <= 5"));
            Assert.True(ScenarioCondition.Evaluate("count != 3"));
            Assert.False(ScenarioCondition.Evaluate("count == 3"));
            Assert.False(ScenarioCondition.Evaluate("count > 5"));
            Assert.False(ScenarioCondition.Evaluate("count < 5"));
        }

        [Fact]
        public void Evaluate_FlagNumeric_LessThan()
        {
            ScenarioCondition.SetFlag("count", "3");
            Assert.True(ScenarioCondition.Evaluate("count < 5"));
            Assert.False(ScenarioCondition.Evaluate("count < 3"));
        }

        [Fact]
        public void Evaluate_FlagNumeric_GreaterThan()
        {
            ScenarioCondition.SetFlag("count", "7");
            Assert.True(ScenarioCondition.Evaluate("count > 5"));
            Assert.False(ScenarioCondition.Evaluate("count > 7"));
        }

        [Fact]
        public void Evaluate_FlagString_NonNumericComparisonFails()
        {
            ScenarioCondition.SetFlag("text", "hello");
            Assert.False(ScenarioCondition.Evaluate("text > 1"));
            Assert.False(ScenarioCondition.Evaluate("text < 10"));
        }

        [Fact]
        public void Evaluate_FlagString_StringComparisonWorks()
        {
            ScenarioCondition.SetFlag("text", "hello");
            Assert.True(ScenarioCondition.Evaluate("text == \"hello\""));
            Assert.True(ScenarioCondition.Evaluate("text != \"world\""));
        }

        [Fact]
        public void Evaluate_GarbageInput_ReturnsFalse()
        {
            Assert.False(ScenarioCondition.Evaluate("not a valid condition @@@@"));
        }

        [Fact]
        public void Evaluate_And_BothTrue_ReturnsTrue()
        {
            ScenarioCondition.SetFlag("a", "1");
            ScenarioCondition.SetFlag("b", "2");
            Assert.True(ScenarioCondition.Evaluate("a == \"1\" && b == \"2\""));
        }

        [Fact]
        public void Evaluate_And_FirstFalse_ReturnsFalse()
        {
            ScenarioCondition.SetFlag("a", "0");
            ScenarioCondition.SetFlag("b", "2");
            Assert.False(ScenarioCondition.Evaluate("a == \"1\" && b == \"2\""));
        }

        [Fact]
        public void Evaluate_And_SecondFalse_ReturnsFalse()
        {
            ScenarioCondition.SetFlag("a", "1");
            ScenarioCondition.SetFlag("b", "0");
            Assert.False(ScenarioCondition.Evaluate("a == \"1\" && b == \"2\""));
        }

        [Fact]
        public void Evaluate_Or_BothTrue_ReturnsTrue()
        {
            ScenarioCondition.SetFlag("a", "1");
            ScenarioCondition.SetFlag("b", "2");
            Assert.True(ScenarioCondition.Evaluate("a == \"1\" || b == \"2\""));
        }

        [Fact]
        public void Evaluate_Or_FirstTrue_ReturnsTrue()
        {
            ScenarioCondition.SetFlag("a", "1");
            ScenarioCondition.SetFlag("b", "0");
            Assert.True(ScenarioCondition.Evaluate("a == \"1\" || b == \"2\""));
        }

        [Fact]
        public void Evaluate_Or_SecondTrue_ReturnsTrue()
        {
            ScenarioCondition.SetFlag("a", "0");
            ScenarioCondition.SetFlag("b", "2");
            Assert.True(ScenarioCondition.Evaluate("a == \"1\" || b == \"2\""));
        }

        [Fact]
        public void Evaluate_Or_BothFalse_ReturnsFalse()
        {
            ScenarioCondition.SetFlag("a", "0");
            ScenarioCondition.SetFlag("b", "0");
            Assert.False(ScenarioCondition.Evaluate("a == \"1\" || b == \"2\""));
        }

        [Fact]
        public void Evaluate_Not_True_ReturnsFalse()
        {
            ScenarioCondition.SetFlag("a", "1");
            Assert.False(ScenarioCondition.Evaluate("!a == \"1\""));
        }

        [Fact]
        public void Evaluate_Not_False_ReturnsTrue()
        {
            ScenarioCondition.SetFlag("a", "0");
            Assert.True(ScenarioCondition.Evaluate("!a == \"1\""));
        }

        [Fact]
        public void Evaluate_DoubleNot_True_ReturnsTrue()
        {
            ScenarioCondition.SetFlag("a", "1");
            Assert.True(ScenarioCondition.Evaluate("!!a == \"1\""));
        }

        [Fact]
        public void Evaluate_Parentheses_GroupingWorks()
        {
            ScenarioCondition.SetFlag("a", "1");
            ScenarioCondition.SetFlag("b", "2");
            Assert.True(ScenarioCondition.Evaluate("a == \"1\" || (b == \"2\" && a == \"1\")"));
            ScenarioCondition.SetFlag("a", "0");
            Assert.False(ScenarioCondition.Evaluate("(a == \"1\" || b == \"2\") && a == \"1\""));
        }

        [Fact]
        public void Evaluate_TrueLiteral_ReturnsTrue()
        {
            Assert.True(ScenarioCondition.Evaluate("true"));
        }

        [Fact]
        public void Evaluate_FalseLiteral_ReturnsFalse()
        {
            Assert.False(ScenarioCondition.Evaluate("false"));
        }

        [Fact]
        public void Evaluate_NotTrue_ReturnsFalse()
        {
            Assert.False(ScenarioCondition.Evaluate("!true"));
        }

        [Fact]
        public void Evaluate_NotFalse_ReturnsTrue()
        {
            Assert.True(ScenarioCondition.Evaluate("!false"));
        }

        [Fact]
        public void Evaluate_TrueAndFalse_ReturnsFalse()
        {
            Assert.False(ScenarioCondition.Evaluate("true && false"));
        }

        [Fact]
        public void Evaluate_TrueOrFalse_ReturnsTrue()
        {
            Assert.True(ScenarioCondition.Evaluate("true || false"));
        }

        [Fact]
        public void Evaluate_TrueAndFlagMatch_ReturnsTrue()
        {
            ScenarioCondition.SetFlag("testFlag", "1");
            Assert.True(ScenarioCondition.Evaluate("true && testFlag == \"1\""));
        }

        [Fact]
        public void Evaluate_FalseOrFlagMatch_ReturnsTrue()
        {
            ScenarioCondition.SetFlag("testFlag", "ok");
            Assert.True(ScenarioCondition.Evaluate("false || testFlag == \"ok\""));
        }

        [Fact]
        public void Evaluate_SameConditionTwice_ConsistentResult()
        {
            ScenarioCondition.SetFlag("cacheFlag", "5");
            var first = ScenarioCondition.Evaluate("cacheFlag >= 5");
            var second = ScenarioCondition.Evaluate("cacheFlag >= 5");
            Assert.Equal(first, second);
        }

        [Fact]
        public void Evaluate_ParseError_NotCached_ReturnsFalseEveryTime()
        {
            Assert.False(ScenarioCondition.Evaluate("invalid condition ("));
            Assert.False(ScenarioCondition.Evaluate("invalid condition ("));
        }

        [Fact]
        public void Evaluate_AfterFlagChange_ReturnsNewResult()
        {
            ScenarioCondition.SetFlag("dynamicFlag", "1");
            Assert.True(ScenarioCondition.Evaluate("dynamicFlag == \"1\""));
            ScenarioCondition.SetFlag("dynamicFlag", "2");
            Assert.False(ScenarioCondition.Evaluate("dynamicFlag == \"1\""));
        }

        [Fact]
        public void ClearCache_CachedCondition_EvaluatesLiveStateAgain()
        {
            ScenarioCondition.SetFlag("clearCacheFlag", "on");
            Assert.True(ScenarioCondition.Evaluate("clearCacheFlag == \"on\" && true"));

            ScenarioCondition.ClearCache();
            Assert.True(ScenarioCondition.Evaluate("clearCacheFlag == \"on\" && true"));

            ScenarioCondition.SetFlag("clearCacheFlag", "off");
            ScenarioCondition.ClearCache();
            Assert.False(ScenarioCondition.Evaluate("clearCacheFlag == \"on\" && true"));
        }

        [Fact]
        public void ClearCache_EmptyCache_DoesNotBreakEvaluation()
        {
            ScenarioCondition.ClearCache();
            ScenarioCondition.ClearCache();
            Assert.True(ScenarioCondition.Evaluate("true"));
            Assert.False(ScenarioCondition.Evaluate("invalid condition ("));
        }
    }
}
