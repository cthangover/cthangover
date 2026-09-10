using System.Reflection;
using Cthangover.Core.Quests;
using Cthangover.Core.Scenes;

namespace Engine.Tests.Scenarios
{
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
        public void Evaluate_UnknownQuestProperty_ReturnsFalse()
        {
            RegisterTestQuest("TestQ", QuestStatus.NotStarted);
            Assert.False(ScenarioCondition.Evaluate("TestQ.UnknownProp == 1"));
        }

        [Fact]
        public void Evaluate_ParseError_ReturnsFalse()
        {
            Assert.False(ScenarioCondition.Evaluate("Quest.hasTag(\"x\""));
        }

        [Fact]
        public void Evaluate_QuestMissing_ReturnsFalse()
        {
            Assert.False(ScenarioCondition.Evaluate("NonExistentQuest.hasTag(\"x\")"));
        }

        [Fact]
        public void Evaluate_QuestMissing_IsActive_ReturnsFalse()
        {
            Assert.False(ScenarioCondition.Evaluate("NonExistentQuest.IsActive"));
        }

        [Fact]
        public void Evaluate_IsActive_Progress_ReturnsTrue()
        {
            RegisterTestQuest("ActiveQ", QuestStatus.Progress);
            Assert.True(ScenarioCondition.Evaluate("ActiveQ.IsActive"));
        }

        [Fact]
        public void Evaluate_IsActive_NotStarted_ReturnsFalse()
        {
            RegisterTestQuest("InactiveQ", QuestStatus.NotStarted);
            Assert.False(ScenarioCondition.Evaluate("InactiveQ.IsActive"));
        }

        [Fact]
        public void Evaluate_IsActive_End_ReturnsFalse()
        {
            RegisterTestQuest("DoneQ", QuestStatus.End);
            Assert.False(ScenarioCondition.Evaluate("DoneQ.IsActive"));
        }

        [Fact]
        public void Evaluate_HasTag_TagPresent_ReturnsTrue()
        {
            var quest = RegisterTestQuest("TagQ", QuestStatus.NotStarted);
            quest.AddTag("found");
            Assert.True(ScenarioCondition.Evaluate("TagQ.hasTag(\"found\")"));
        }

        [Fact]
        public void Evaluate_HasTag_TagAbsent_ReturnsFalse()
        {
            var quest = RegisterTestQuest("TagQ2", QuestStatus.NotStarted);
            quest.AddTag("other");
            Assert.False(ScenarioCondition.Evaluate("TagQ2.hasTag(\"missing\")"));
        }

        [Fact]
        public void Evaluate_NotHasTag_TagPresent_ReturnsFalse()
        {
            var quest = RegisterTestQuest("TagQ3", QuestStatus.NotStarted);
            quest.AddTag("present");
            Assert.False(ScenarioCondition.Evaluate("TagQ3.notHasTag(\"present\")"));
        }

        [Fact]
        public void Evaluate_NotHasTag_TagAbsent_ReturnsTrue()
        {
            RegisterTestQuest("TagQ4", QuestStatus.NotStarted);
            Assert.True(ScenarioCondition.Evaluate("TagQ4.notHasTag(\"absent\")"));
        }

        [Fact]
        public void Evaluate_Status_Progress_ReturnsTrue()
        {
            RegisterTestQuest("StatusQ", QuestStatus.Progress);
            Assert.True(ScenarioCondition.Evaluate("StatusQ.Status == 1"));
        }

        [Fact]
        public void Evaluate_Status_NotStarted_ReturnsTrue()
        {
            RegisterTestQuest("StatusQ2", QuestStatus.NotStarted);
            Assert.True(ScenarioCondition.Evaluate("StatusQ2.Status == 0"));
        }

        [Fact]
        public void Evaluate_Status_End_ReturnsTrue()
        {
            RegisterTestQuest("StatusQ3", QuestStatus.End);
            Assert.True(ScenarioCondition.Evaluate("StatusQ3.Status == 2"));
        }

        [Fact]
        public void Evaluate_DataStatus_Progress_ReturnsTrue()
        {
            var quest = RegisterTestQuest("DataQ", QuestStatus.NotStarted);
            quest.Data.SetStatus(1);
            Assert.True(ScenarioCondition.Evaluate("DataQ.DataStatus == 1"));
        }

        [Fact]
        public void Evaluate_DataStatus_Zero_ReturnsTrue()
        {
            var quest = RegisterTestQuest("DataQ2", QuestStatus.NotStarted);
            quest.Data.SetStatus(0);
            Assert.True(ScenarioCondition.Evaluate("DataQ2.DataStatus == 0"));
        }

        [Fact]
        public void Evaluate_DataStatus_AllComparisons()
        {
            var quest = RegisterTestQuest("DataQ3", QuestStatus.NotStarted);
            quest.Data.SetStatus(5);
            Assert.True(ScenarioCondition.Evaluate("DataQ3.DataStatus == 5"));
            Assert.True(ScenarioCondition.Evaluate("DataQ3.DataStatus >= 5"));
            Assert.True(ScenarioCondition.Evaluate("DataQ3.DataStatus <= 5"));
            Assert.True(ScenarioCondition.Evaluate("DataQ3.DataStatus != 3"));
            Assert.True(ScenarioCondition.Evaluate("DataQ3.DataStatus > 4"));
            Assert.True(ScenarioCondition.Evaluate("DataQ3.DataStatus < 6"));
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
        public void Evaluate_IsActiveAndHasTag_QuestActive_ReturnsTrue()
        {
            var quest = RegisterTestQuest("ComplexQ", QuestStatus.Progress);
            quest.AddTag("go_home");
            Assert.True(ScenarioCondition.Evaluate("ComplexQ.IsActive && ComplexQ.hasTag(\"go_home\")"));
        }

        [Fact]
        public void Evaluate_IsActiveAndHasTag_QuestNotActive_ReturnsFalse()
        {
            var quest = RegisterTestQuest("ComplexQ2", QuestStatus.NotStarted);
            quest.AddTag("go_home");
            Assert.False(ScenarioCondition.Evaluate("ComplexQ2.IsActive && ComplexQ2.hasTag(\"go_home\")"));
        }

        [Fact]
        public void Evaluate_IsActiveAndHasTag_TagMissing_ReturnsFalse()
        {
            RegisterTestQuest("ComplexQ3", QuestStatus.Progress);
            Assert.False(ScenarioCondition.Evaluate("ComplexQ3.IsActive && ComplexQ3.hasTag(\"go_home\")"));
        }

        [Fact]
        public void Evaluate_HasTagAndNotHasTag_BothSatisfied_ReturnsTrue()
        {
            var quest = RegisterTestQuest("GateQ", QuestStatus.Progress);
            quest.AddTag("wolf_battle");
            Assert.True(ScenarioCondition.Evaluate("GateQ.hasTag(\"wolf_battle\") && GateQ.notHasTag(\"boss_battle\")"));
        }

        [Fact]
        public void Evaluate_HasTagAndNotHasTag_HasBoth_ReturnsFalse()
        {
            var quest = RegisterTestQuest("GateQ2", QuestStatus.Progress);
            quest.AddTag("wolf_battle");
            quest.AddTag("boss_battle");
            Assert.False(ScenarioCondition.Evaluate("GateQ2.hasTag(\"wolf_battle\") && GateQ2.notHasTag(\"boss_battle\")"));
        }

        [Fact]
        public void Evaluate_StatusNotEqualZero_ReturnsTrueWhenProgress()
        {
            RegisterTestQuest("ProgressQ", QuestStatus.Progress);
            Assert.True(ScenarioCondition.Evaluate("ProgressQ.Status != 0"));
        }

        [Fact]
        public void Evaluate_StatusNotEqualZero_ReturnsFalseWhenNotStarted()
        {
            RegisterTestQuest("NewQ", QuestStatus.NotStarted);
            Assert.False(ScenarioCondition.Evaluate("NewQ.Status != 0"));
        }

        [Fact]
        public void Evaluate_FlagAndQuestConditionMixed()
        {
            ScenarioCondition.SetFlag("ready", "1");
            var quest = RegisterTestQuest("MixedQ", QuestStatus.Progress);
            quest.AddTag("stage1");
            Assert.True(ScenarioCondition.Evaluate("ready == \"1\" && MixedQ.hasTag(\"stage1\")"));
        }

        [Fact]
        public void Evaluate_NotQuestCondition()
        {
            RegisterTestQuest("NotQ", QuestStatus.NotStarted);
            Assert.True(ScenarioCondition.Evaluate("!NotQ.IsActive"));
        }

        [Fact]
        public void Evaluate_QuestEnd_StatusEqualsTwo()
        {
            RegisterTestQuest("EndQ", QuestStatus.End);
            Assert.True(ScenarioCondition.Evaluate("EndQ.Status == 2"));
        }

        [Fact]
        public void Evaluate_ProgressViaDataStatus()
        {
            var quest = RegisterTestQuest("DataStatusQ", QuestStatus.NotStarted);
            quest.Data.SetStatus(3);
            Assert.True(ScenarioCondition.Evaluate("DataStatusQ.DataStatus >= 2"));
            Assert.False(ScenarioCondition.Evaluate("DataStatusQ.DataStatus == 0"));
        }

        private static QuestBase RegisterTestQuest(string id, QuestStatus status)
        {
            var quest = new QuestBase
            {
                ID = id,
                Name = $"Test {id}",
            };
            quest.Status = status;

            var factoryType = typeof(QuestFactory);
            var instanceField = factoryType.GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            var lazyValue = instanceField.GetValue(null);
            var factory = lazyValue.GetType().GetProperty("Value").GetValue(lazyValue);

            var isLoadedField = factoryType.GetField("isLoaded", BindingFlags.Instance | BindingFlags.NonPublic);
            isLoadedField.SetValue(factory, true);

            var dataField = factoryType.GetField("dataByID", BindingFlags.Instance | BindingFlags.NonPublic);
            var dict = (IDictionary<string, QuestBase>)dataField.GetValue(factory);
            dict[id] = quest;

            return quest;
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
            Assert.False(ScenarioCondition.Evaluate("Quest.hasTag(\"x\""));
            Assert.False(ScenarioCondition.Evaluate("Quest.hasTag(\"x\""));
        }

        [Fact]
        public void Evaluate_AfterFlagChange_ReturnsNewResult()
        {
            ScenarioCondition.SetFlag("dynamicFlag", "1");
            Assert.True(ScenarioCondition.Evaluate("dynamicFlag == \"1\""));
            ScenarioCondition.SetFlag("dynamicFlag", "2");
            Assert.False(ScenarioCondition.Evaluate("dynamicFlag == \"1\""));
        }
    }
}
