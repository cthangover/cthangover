using Cthangover.Core.Quests;
using Engine.Tests.Scenarios;

namespace Engine.Tests.Quests
{
    /// <summary>
    /// Covers <see cref="QuestTagLocale"/>: the composite key built from a
    /// quest's locale prefix and tag, and the translation lookup with its
    /// raw-tag fallback. The journal stamps are the only consumer, so a
    /// regression here surfaces as untranslated or wrongly namespaced seals.
    /// </summary>
    public class QuestTagLocaleTests
    {
        private readonly QuestBase _quest = new QuestBase
        {
            ID = "AwakeningQuest",
            Name = "quest/awakening",
        };

        [Fact]
        public void Key_ComposesQuestPrefixAndTag()
        {
            Assert.Equal("quest/awakening/tag/cloak_taken", QuestTagLocale.Key(_quest, "cloak_taken"));
        }

        [Fact]
        public void Key_NullQuest_ReturnsNull()
        {
            Assert.Null(QuestTagLocale.Key(null, "cloak_taken"));
        }

        [Fact]
        public void Key_EmptyTag_ReturnsNull()
        {
            Assert.Null(QuestTagLocale.Key(_quest, ""));
            Assert.Null(QuestTagLocale.Key(_quest, null));
        }

        [Fact]
        public void Key_QuestWithoutLocalePrefix_ReturnsNull()
        {
            Assert.Null(QuestTagLocale.Key(new QuestBase { ID = "NoName" }, "cloak_taken"));
        }

        [Fact]
        public void Translate_UsesCompositeKey_NotRawTag()
        {
            var locale = new TestLocale();
            locale.Set("cloak_taken", "Raw");
            locale.Set("quest/awakening/tag/cloak_taken", "Cloak found");

            Assert.Equal("Cloak found", QuestTagLocale.Translate(_quest, "cloak_taken", locale));
        }

        [Fact]
        public void Translate_MissingKey_ReturnsRawTag()
        {
            Assert.Equal("cloak_taken", QuestTagLocale.Translate(_quest, "cloak_taken", new TestLocale()));
        }

        [Fact]
        public void Translate_NullQuest_ReturnsRawTag()
        {
            Assert.Equal("cloak_taken", QuestTagLocale.Translate(null, "cloak_taken", new TestLocale()));
        }

        [Fact]
        public void Translate_NullProvider_ReturnsRawTag()
        {
            Assert.Equal("cloak_taken", QuestTagLocale.Translate(_quest, "cloak_taken", null));
        }
    }
}
