using Cthangover.Core.UI.Messages;
using Cthangover.Core.Quests;
using Godot;

namespace Cthangover.Core.Quests
{
    public static class IQuestExtensions
    {
        public static void SendNotification(this IQuest quest)
        {
            MessagesHelper.AddMessage(TranslationServer.Translate("ui/journal/new") + " - " + TranslationServer.Translate(quest.Name));
        }
    }
}
