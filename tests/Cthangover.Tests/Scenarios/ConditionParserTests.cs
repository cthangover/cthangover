using Cthangover.Core.Scenes;
using Cthangover.Core.Scenes.Nodes.Impls;

namespace Engine.Tests.Scenarios
{
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
        public void Parse_HasTag_ReturnsQuestMethodCondition()
        {
            var node = ConditionParser.Parse("Quest.hasTag(\"started\")");
            var qm = Assert.IsType<QuestMethodCondition>(node);
            Assert.Equal("Quest", qm.QuestId);
            Assert.Equal("hasTag", qm.Method);
            Assert.Equal("started", qm.Tag);
        }

        [Fact]
        public void Parse_NotHasTag_ReturnsQuestMethodCondition()
        {
            var node = ConditionParser.Parse("Quest.notHasTag(\"finished\")");
            var qm = Assert.IsType<QuestMethodCondition>(node);
            Assert.Equal("Quest", qm.QuestId);
            Assert.Equal("notHasTag", qm.Method);
            Assert.Equal("finished", qm.Tag);
        }

        [Fact]
        public void Parse_StatusEqual_ReturnsQuestPropCondition()
        {
            var node = ConditionParser.Parse("Quest.Status == 1");
            var qp = Assert.IsType<QuestPropCondition>(node);
            Assert.Equal("Quest", qp.QuestId);
            Assert.Equal("Status", qp.Property);
            Assert.Equal("==", qp.Op);
            Assert.Equal(1, qp.Value);
        }

        [Fact]
        public void Parse_StatusNotEqual_ReturnsQuestPropCondition()
        {
            var node = ConditionParser.Parse("Quest.Status != 2");
            var qp = Assert.IsType<QuestPropCondition>(node);
            Assert.Equal("Quest", qp.QuestId);
            Assert.Equal("Status", qp.Property);
            Assert.Equal("!=", qp.Op);
            Assert.Equal(2, qp.Value);
        }

        [Fact]
        public void Parse_StatusGreaterOrEqual_ReturnsQuestPropCondition()
        {
            var node = ConditionParser.Parse("Quest.Status >= 1");
            var qp = Assert.IsType<QuestPropCondition>(node);
            Assert.Equal(">=", qp.Op);
            Assert.Equal(1, qp.Value);
        }

        [Fact]
        public void Parse_StatusLessOrEqual_ReturnsQuestPropCondition()
        {
            var node = ConditionParser.Parse("Quest.Status <= 2");
            var qp = Assert.IsType<QuestPropCondition>(node);
            Assert.Equal("<=", qp.Op);
            Assert.Equal(2, qp.Value);
        }

        [Fact]
        public void Parse_StatusGreater_ReturnsQuestPropCondition()
        {
            var node = ConditionParser.Parse("Quest.Status > 0");
            var qp = Assert.IsType<QuestPropCondition>(node);
            Assert.Equal(">", qp.Op);
            Assert.Equal(0, qp.Value);
        }

        [Fact]
        public void Parse_StatusLess_ReturnsQuestPropCondition()
        {
            var node = ConditionParser.Parse("Quest.Status < 2");
            var qp = Assert.IsType<QuestPropCondition>(node);
            Assert.Equal("<", qp.Op);
            Assert.Equal(2, qp.Value);
        }

        [Fact]
        public void Parse_DataStatusAllOperators_ReturnsQuestPropCondition()
        {
            var ops = new[] { "==", "!=", ">=", "<=", ">", "<" };
            foreach (var op in ops)
            {
                var node = ConditionParser.Parse($"Quest.DataStatus {op} 5");
                var qp = Assert.IsType<QuestPropCondition>(node);
                Assert.Equal("DataStatus", qp.Property);
                Assert.Equal(op, qp.Op);
                Assert.Equal(5, qp.Value);
            }
        }

        [Fact]
        public void Parse_IsActive_ReturnsQuestBoolPropCondition()
        {
            var node = ConditionParser.Parse("Quest.IsActive");
            var qbp = Assert.IsType<QuestBoolPropCondition>(node);
            Assert.Equal("Quest", qbp.QuestId);
            Assert.Equal("IsActive", qbp.Property);
        }

        [Fact]
        public void Parse_SimpleAnd_ReturnsAndNode()
        {
            var node = ConditionParser.Parse("Quest.hasTag(\"a\") && Quest.hasTag(\"b\")");
            var bin = Assert.IsType<AndNode>(node);
            Assert.IsType<QuestMethodCondition>(bin.Left);
            Assert.IsType<QuestMethodCondition>(bin.Right);
        }

        [Fact]
        public void Parse_SimpleOr_ReturnsOrNode()
        {
            var node = ConditionParser.Parse("Quest.hasTag(\"a\") || Quest.hasTag(\"b\")");
            var bin = Assert.IsType<OrNode>(node);
            Assert.IsType<QuestMethodCondition>(bin.Left);
            Assert.IsType<QuestMethodCondition>(bin.Right);
        }

        [Fact]
        public void Parse_Not_ReturnsNotNode()
        {
            var node = ConditionParser.Parse("!Quest.hasTag(\"x\")");
            var un = Assert.IsType<NotNode>(node);
            Assert.IsType<QuestMethodCondition>(un.Operand);
        }

        [Fact]
        public void Parse_DoubleNot_ReturnsUnaryOfUnary()
        {
            var node = ConditionParser.Parse("!!Quest.hasTag(\"x\")");
            var un = Assert.IsType<NotNode>(node);
            var inner = Assert.IsType<NotNode>(un.Operand);
            Assert.IsType<QuestMethodCondition>(inner.Operand);
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
            var node = ConditionParser.Parse("a.hasTag(\"1\") && b.hasTag(\"2\") && c.hasTag(\"3\")");
            var bin = Assert.IsType<AndNode>(node);
            var left = Assert.IsType<AndNode>(bin.Left);
            Assert.IsType<QuestMethodCondition>(bin.Right);
        }

        [Fact]
        public void Parse_CompoundOrOr_ThreeTerms()
        {
            var node = ConditionParser.Parse("a.hasTag(\"1\") || b.hasTag(\"2\") || c.hasTag(\"3\")");
            var bin = Assert.IsType<OrNode>(node);
            var left = Assert.IsType<OrNode>(bin.Left);
            Assert.IsType<QuestMethodCondition>(bin.Right);
        }

        [Fact]
        public void Parse_IsActiveAndHasTag_ReturnsCorrectTree()
        {
            var node = ConditionParser.Parse("Quest.IsActive && Quest.hasTag(\"go_home\")");
            var bin = Assert.IsType<AndNode>(node);
            Assert.IsType<QuestBoolPropCondition>(bin.Left);
            Assert.IsType<QuestMethodCondition>(bin.Right);
        }

        [Fact]
        public void Parse_HasTagAndNotHasTag_ReturnsCorrectTree()
        {
            var node = ConditionParser.Parse("Quest.hasTag(\"wolf_battle\") && Quest.notHasTag(\"boss_battle\")");
            var bin = Assert.IsType<AndNode>(node);
            var left = Assert.IsType<QuestMethodCondition>(bin.Left);
            Assert.Equal("hasTag", left.Method);
            var right = Assert.IsType<QuestMethodCondition>(bin.Right);
            Assert.Equal("notHasTag", right.Method);
        }

        [Fact]
        public void Parse_FlagAndStatusMixed_ReturnsCorrectTree()
        {
            var node = ConditionParser.Parse("flag == \"1\" && Quest.Status >= 1");
            var bin = Assert.IsType<AndNode>(node);
            Assert.IsType<FlagCondition>(bin.Left);
            Assert.IsType<QuestPropCondition>(bin.Right);
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
        public void Parse_IsActiveWithoutParens_Works()
        {
            var node = ConditionParser.Parse("Quest.IsActive");
            var qbp = Assert.IsType<QuestBoolPropCondition>(node);
            Assert.Equal("Quest", qbp.QuestId);
            Assert.Equal("IsActive", qbp.Property);
        }

        [Fact]
        public void Parse_IsActiveOrHasTag_Works()
        {
            var node = ConditionParser.Parse("Quest.IsActive || Quest.hasTag(\"ready\")");
            var bin = Assert.IsType<OrNode>(node);
            Assert.IsType<QuestBoolPropCondition>(bin.Left);
            Assert.IsType<QuestMethodCondition>(bin.Right);
        }

        [Fact]
        public void Parse_NotIsActive_Works()
        {
            var node = ConditionParser.Parse("!Quest.IsActive");
            var un = Assert.IsType<NotNode>(node);
            Assert.IsType<QuestBoolPropCondition>(un.Operand);
        }

        [Fact]
        public void Parse_UnknownQuestProperty_ThrowsFormatException()
        {
            var ex = Assert.Throws<FormatException>(() => ConditionParser.Parse("Quest.UnknownProp == 1"));
            Assert.Contains("unknown quest property/method", ex.Message);
        }

        [Fact]
        public void Parse_UnknownQuestMethod_ThrowsFormatException()
        {
            var ex = Assert.Throws<FormatException>(() => ConditionParser.Parse("Quest.someMethod(\"x\")"));
            Assert.Contains("unknown quest property/method", ex.Message);
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
        public void Parse_HasTagMissingCloseParen_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ConditionParser.Parse("Quest.hasTag(\"x\""));
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
        public void Parse_StatusWithoutValue_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ConditionParser.Parse("Quest.Status =="));
        }

        [Fact]
        public void Parse_DataStatusWithoutValue_ThrowsFormatException()
        {
            Assert.Throws<FormatException>(() => ConditionParser.Parse("Quest.DataStatus >="));
        }

        [Fact]
        public void Parse_ExplosiveCondition_AllOperators()
        {
            var node = ConditionParser.Parse(
                "Quest.hasTag(\"a\") && (Quest.Status >= 1 || Quest.IsActive) && flag != \"skip\"");
            var bin = Assert.IsType<AndNode>(node);
            var left = Assert.IsType<AndNode>(bin.Left);
            Assert.IsType<QuestMethodCondition>(left.Left);
            var parens = Assert.IsType<OrNode>(left.Right);
            Assert.IsType<QuestPropCondition>(parens.Left);
            Assert.IsType<QuestBoolPropCondition>(parens.Right);
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
        public void Parse_TrueAsQuestId_StillWorks()
        {
            var node = ConditionParser.Parse("true.IsActive");
            Assert.IsType<QuestBoolPropCondition>(node);
        }

        [Fact]
        public void Parse_FalseAsQuestId_StillWorks()
        {
            var node = ConditionParser.Parse("false.hasTag(\"test\")");
            Assert.IsType<QuestMethodCondition>(node);
        }
    }
}
