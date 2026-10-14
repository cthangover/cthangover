using Cthangover.Core.Localization;

namespace Cthangover.Core.Quests
{
    /// <summary>
    /// Maps quest tags to locale keys and resolves their display text. A bare
    /// tag id is only unique inside its quest — the same word may mean
    /// different milestones in different quests — so the key is composed from
    /// the quest's locale prefix (<see cref="IQuest.Name"/>) and the tag:
    /// <c>{quest.Name}/tag/{tag}</c>. For
    /// <c>MeetingMurakamiQuest</c> (<c>Name = quest/meeting/murakami</c>) the
    /// tag <c>go_home</c> maps to <c>quest/meeting/murakami/tag/go_home</c>,
    /// sharing the namespace of the quest's own name and stage descriptions.
    /// The journal renders every earned tag as a stamp through
    /// <see cref="Translate(IQuest, string)"/>; when no translation exists the
    /// raw tag id is shown, which is the behaviour tags had before locale
    /// support was added.
    /// </summary>
    public static class QuestTagLocale
    {
        /// <summary>
        /// Builds the composite locale key for <paramref name="tag"/> inside
        /// <paramref name="quest"/>: <c>{quest.Name}/tag/{tag}</c>. Returns
        /// <c>null</c> when the quest is missing, has no locale prefix, or the
        /// tag is empty, so callers can distinguish "no key to look up" from
        /// "key not translated" and fall back to the raw tag.
        /// </summary>
        public static string Key(IQuest quest, string tag)
        {
            if (quest == null || string.IsNullOrEmpty(quest.Name) || string.IsNullOrEmpty(tag))
                return null;

            return quest.Name + "/tag/" + tag;
        }

        /// <summary>
        /// Returns the display text of <paramref name="tag"/> in the player's
        /// current language, falling back to the raw tag id when the composite
        /// key is absent from all loaded mods' properties files.
        /// </summary>
        public static string Translate(IQuest quest, string tag)
        {
            return Translate(quest, tag, GodotTranslationProvider.Instance);
        }

        /// <summary>
        /// Provider-injected variant of <see cref="Translate(IQuest, string)"/>
        /// for tests and for callers that already hold an
        /// <see cref="ILocalizationProvider"/>. A provider that returns
        /// <c>null</c> for the composite key (the "untranslated" signal of
        /// <see cref="ILocalizationProvider"/>) also yields the raw tag.
        /// </summary>
        public static string Translate(IQuest quest, string tag, ILocalizationProvider provider)
        {
            var key = Key(quest, tag);
            if (key == null || provider == null)
                return tag;

            return provider.Get(key) ?? tag;
        }
    }
}