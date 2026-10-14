using Cthangover.Core.Scenes;
using Cthangover.Core.Scenes.Nodes.Impls;

namespace Engine.Tests.Scenarios
{
    /// <summary>
    /// Unit coverage for the core condition grammar: flag comparisons, boolean
    /// literals, and/or/not structure, precedence, and error cases. Quest
    /// grammar (Quest.hasTag / Quest.Status / Quest.IsActive) is provided by the
    /// quests_base mod's QuestParser leaf parser and covered by the integration
    /// coverage_quests test - the core kernel must not auto-register it (see
    /// Parse_QuestGrammar_NotRegisteredInCore_Throws at the bottom).
    /// </summary>
    public class ConditionParserTests
    {
        [Fact]
        public void Parse_Null_ReturnsNull()
        {
            Assert.Null(ConditionParser.Parse(null));
        }

        [Fact]
        public void Parse_EmptyString_ReturnsNull()
        {
            Assert.Null(ConditionParser.Parse(""));
        }

        [Fact]
        public void Parse_Whitespace_ReturnsNull()
        {
            Assert.Null(ConditionParser.Parse("   \t  "));
        }

        [Fact]
        public void Parse_FlagEqualString_ReturnsFlagCondition()
        {
            var node = ConditionParser.Parse("myFlag == \"done\"");
            var fc = Assert.IsType<FlagCondition>(node);
            Assert.Equal("myFlag", fc.Key);
            Assert.Equal("==", fc.Op);
            Assert.Equal("done", fc.Value);
        }

        [Fact]
        public void Parse_FlagNotEqualNumber_ReturnsFlagCondition()
        {
            var node = ConditionParser.Parse("counter != 42");
            var fc = Assert.IsType<FlagCondition>(node);
            Assert.Equal("counter", fc.Key);
            Assert.Equal("!=", fc.Op);
            Assert.Equal("42", fc.Value);
        }

        [Fact]
        public void Parse_FlagGreaterOrEqual_ReturnsFlagCondition()
        {
            var node = ConditionParser.Parse("level >= 5");
            var fc = Assert.IsType<FlagCondition>(node);
            Assert.Equal("level", fc.Key);
            Assert.Equal(">=", fc.Op);
            Assert.Equal("5", fc.Value);
        }

        [Fact]
        public void Parse_FlagLessOrEqual_ReturnsFlagCondition()
        {
            var node = ConditionParser.Parse("value <= 10");
            var fc = Assert.IsType<FlagCondition>(node);
            Assert.Equal("value", fc.Key);
            Assert.Equal("<=", fc.Op);
            Assert.Equal("10", fc.Value);
        }

        [Fact]
        public void Parse_FlagGreater_ReturnsFlagCondition()
        {
            var node = ConditionParser.Parse("x > 3");
            var fc = Assert.IsType<FlagCondition>(node);
            Assert.Equal("x", fc.Key);
            Assert.Equal(">", fc.Op);
            Assert.Equal("3", fc.Value);
        }

        [Fact]
        public void Parse_FlagLess_ReturnsFlagCondition()
        {
            var node = ConditionParser.Parse("y < 100");
            var fc = Assert.IsType<FlagCondition>(node);
            Assert.Equal("y", fc.Key);
            Assert.Equal("<", fc.Op);
            Assert.Equal("100", fc.Value);
        }

        [Fact]
        public void Parse_FlagWithSingleQuotedString_ReturnsFlagCondition()
        {
            var node = ConditionParser.Parse("key == 'hello'");
            var fc = Assert.IsType<FlagCondition>(node);
            Assert.Equal("hello", fc.Value);
        }

        [Fact]
        public void Parse_SimpleAnd_ReturnsAndNode()
        {
            var node = ConditionParser.Parse("a == \"1\" && b == \"2\"");
            var bin = Assert.IsType<AndNode>(node);
            Assert.IsType<FlagCondition>(bin.Left);
            Assert.IsType<FlagCondition>(bin.Right);
        }

        [Fact]
        public void Parse_SimpleOr_ReturnsOrNode()
        {
            var node = ConditionParser.Parse("a == \"1\" || b == \"2\"");
            var bin = Assert.IsType<OrNode>(node);
            Assert.IsType<FlagCondition>(bin.Left);
            Assert.IsType<FlagCondition>(bin.Right);
        }

        [Fact]
        public void Parse_Not_ReturnsNotNode()
        {
            var node = ConditionParser.Parse("!a == \"1\"");
            var un = Assert.IsType<NotNode>(node);
            Assert.IsType<FlagCondition>(un.Operand);
        }

        [Fact]
        public void Parse_DoubleNot_ReturnsUnaryOfUnary()
        {
            var node = ConditionParser.Parse("!!a == \"1\"");
            var un = Assert.IsType<NotNode>(node);
            var inner = Assert.IsType<NotNode>(un.Operand);
            Assert.IsType<FlagCondition>(inner.Operand);
        }

        [Fact]
        public void Parse_NotFlag_ReturnsNotNode()
        {
            var node = ConditionParser.Parse("!flag == \"done\"");
            var un = Assert.IsType<NotNode>(node);
            Assert.IsType<FlagCondition>(un.Operand);
        }

        [Fact]
        public void Parse_AndPrecedenceOverOr_LeftIsDeep()
        {
            var node = ConditionParser.Parse("a == \"1\" && b == \"2\" || c == \"3\"");
            var bin = Assert.IsType<OrNode>(node);
            var left = Assert.IsType<AndNode>(bin.Left);
            Assert.IsType<FlagCondition>(left.Left);
            Assert.IsType<FlagCondition>(left.Right);
            Assert.IsType<FlagCondition>(bin.Right);
        }

        [Fact]
        public void Parse_OrAndMixed_PrecedenceIsCorrect()
        {
            var node = ConditionParser.Parse("a == \"1\" || b == \"2\" && c == \"3\"");
            var bin = Assert.IsType<OrNode>(node);
            Assert.IsType<FlagCondition>(bin.Left);
            var right = Assert.IsType<AndNode>(bin.Right);
        }

        [Fact]
        public void Parse_ParenthesesOverridePrecedence()
        {
            var node = ConditionParser.Parse("(a == \"1\" || b == \"2\") && c == \"3\"");
            var bin = Assert.IsType<AndNode>(node);
            Assert.IsType<FlagCondition>(bin.Right);
            var left = Assert.IsType<OrNode>(bin.Left);
        }

        [Fact]
        public void Parse_NestedParentheses_ReturnsCorrectStructure()
        {
            var node = ConditionParser.Parse("((a == \"1\"))");
            var fc = Assert.IsType<FlagCondition>(node);
            Assert.Equal("a", fc.Key);
        }

        [Fact]
        public void Parse_FlagWithNot_ReturnsUnaryOfFlagCondition()
        {
            var node = ConditionParser.Parse("!flag == \"x\"");
            var un = Assert.IsType<NotNode>(node);
            var fc = Assert.IsType<FlagCondition>(un.Operand);
            Assert.Equal("flag", fc.Key);
        }

        [Fact]
        public void Parse_CompoundAndAnd_ThreeTerms()
        {
            var node = ConditionParser.Parse("a == \"1\" && b == \"2\" && c == \"3\"");
            var bin = Assert.IsType<AndNode>(node);
            var left = Assert.IsType<AndNode>(bin.Left);
            Assert.IsType<FlagCondition>(bin.Right);
        }

        [Fact]
        public void Parse_CompoundOrOr_ThreeTerms()
        {
            var node = ConditionParser.Parse("a == \"1\" || b == \"2\" || c == \"3\"");
            var bin = Assert.IsType<OrNode>(node);
            var left = Assert.IsType<OrNode>(bin.Left);
            Assert.IsType<FlagCondition>(bin.Right);
        }

        [Fact]
        public void Parse_ComplexNestedNotAndOr()
        {
            var node = ConditionParser.Parse("!a == \"1\" && (b == \"2\" || c == \"3\")");
            var bin = Assert.IsType<AndNode>(node);
            var left = Assert.IsType<NotNode>(bin.Left);
            Assert.IsType<FlagCondition>(left.Operand);
            var right = Assert.IsType<OrNode>(bin.Right);
        }

        [Fact]
        public void Parse_NotWithParentheses_ReturnsUnaryWrapper()
        {
            var node = ConditionParser.Parse("!(a == \"1\" && b == \"2\")");
            var un = Assert.IsType<NotNode>(node);
            var bin = Assert.IsType<AndNode>(un.Operand);
        }

        [Fact]
        public void Parse_AmbiguousOperator_SingleAmpersand_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ConditionParser.Parse("a == \"1\" & b == \"2\""));
        }

        [Fact]
        public void Parse_AmbiguousOperator_SinglePipe_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ConditionParser.Parse("a == \"1\" | b == \"2\""));
        }

        [Fact]
        public void Parse_SingleEquals_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ConditionParser.Parse("a = b"));
        }

        [Fact]
        public void Parse_MissingCloseParen_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ConditionParser.Parse("(a == \"1\""));
        }

        [Fact]
        public void Parse_MissingOpenParen_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ConditionParser.Parse("a == \"1\")"));
        }

        [Fact]
        public void Parse_ExtraGarbageAtEnd_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ConditionParser.Parse("a == \"1\" extra_stuff"));
        }

        [Fact]
        public void Parse_FlagWithoutOperator_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ConditionParser.Parse("flag \"value\""));
        }

        [Fact]
        public void Parse_FlagWithoutValue_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ConditionParser.Parse("flag =="));
        }

        [Fact]
        public void Parse_ExplosiveCondition_AllOperators()
        {
            var node = ConditionParser.Parse(
                "a == \"1\" && (b >= 2 || c != \"3\") && d != \"skip\"");
            var bin = Assert.IsType<AndNode>(node);
            var left = Assert.IsType<AndNode>(bin.Left);
            Assert.IsType<FlagCondition>(left.Left);
            var parens = Assert.IsType<OrNode>(left.Right);
            Assert.IsType<FlagCondition>(parens.Left);
            Assert.IsType<FlagCondition>(parens.Right);
            Assert.IsType<FlagCondition>(bin.Right);
        }

        [Fact]
        public void Parse_TrueLiteral_ReturnsBoolLiteral()
        {
            var node = ConditionParser.Parse("true");
            var bl = Assert.IsType<BoolLiteral>(node);
            Assert.True(bl.Value);
        }

        [Fact]
        public void Parse_FalseLiteral_ReturnsBoolLiteral()
        {
            var node = ConditionParser.Parse("false");
            var bl = Assert.IsType<BoolLiteral>(node);
            Assert.False(bl.Value);
        }

        [Fact]
        public void Parse_NotTrue_ReturnsUnary()
        {
            var node = ConditionParser.Parse("!true");
            var un = Assert.IsType<NotNode>(node);
            var bl = Assert.IsType<BoolLiteral>(un.Operand);
            Assert.True(bl.Value);
        }

        [Fact]
        public void Parse_TrueAndFalse_ReturnsAnd()
        {
            var node = ConditionParser.Parse("true && false");
            var bin = Assert.IsType<AndNode>(node);
            Assert.IsType<BoolLiteral>(bin.Left);
            Assert.IsType<BoolLiteral>(bin.Right);
        }

        [Fact]
        public void Parse_TrueOrFalse_ReturnsOr()
        {
            var node = ConditionParser.Parse("true || false");
            var bin = Assert.IsType<OrNode>(node);
            Assert.IsType<BoolLiteral>(bin.Left);
            Assert.IsType<BoolLiteral>(bin.Right);
        }

        [Fact]
        public void Parse_ParenthesizedTrue_ReturnsBoolLiteral()
        {
            var node = ConditionParser.Parse("(true)");
            var bl = Assert.IsType<BoolLiteral>(node);
            Assert.True(bl.Value);
        }

        [Fact]
        public void Parse_TrueWithFlag_ReturnsAnd()
        {
            var node = ConditionParser.Parse("true && flag == 1");
            var bin = Assert.IsType<AndNode>(node);
            Assert.IsType<BoolLiteral>(bin.Left);
            Assert.IsType<FlagCondition>(bin.Right);
        }

        [Fact]
        public void Parse_QuestGrammar_NotRegisteredInCore_Throws()
        {
            // QuestParser is a quests_base leaf parser registered by the mod at
            // load time. The core kernel must not auto-register it, so quest
            // grammar fails to parse in a core-only test run. The actual quest
            // grammar is exercised by the integration coverage_quests test.
            Assert.Throws<FormatException>(() => ConditionParser.Parse("Quest.Status == 1"));
            Assert.Throws<FormatException>(() => ConditionParser.Parse("Quest.hasTag(\"x\")"));
            Assert.Throws<FormatException>(() => ConditionParser.Parse("Quest.IsActive"));
        }
    }
}
