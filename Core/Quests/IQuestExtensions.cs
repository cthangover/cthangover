using Cthangover.Core.UI.Messages;
using Cthangover.Core.Quests;
using Godot;

namespace Cthangover.Core.Quests
{
    /// <summary>
    /// Extension methods layered on top of <see cref="IQuest"/> so that
    /// dialogue scripts and event handlers can trigger common side-effects
    /// (such as notification banner display) without forcing every quest
    /// implementation to carry UI dependencies.
    /// </summary>
    public static class IQuestExtensions
    {
        /// <summary>
        /// Posts a localised "New Journal Entry" notification to the message
        /// queue via <see cref="MessagesHelper"/>. The notification text
        /// prepends the translation of the <c>"ui/journal/new"</c> key to the
        /// translated quest <see cref="IQuest.Name"/>, producing a
        /// journal‑style announcement that the in‑game HUD renders as a
        /// floating banner.
        /// </summary>
        public static void SendNotification(this IQuest quest)
        {
            MessagesHelper.AddMessage(TranslationServer.Translate("ui/journal/new") + " - " + TranslationServer.Translate(quest.Name));
        }
    }
}
