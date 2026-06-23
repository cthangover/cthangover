using System;
using System.Collections.Generic;

namespace Cthangover.Core.Quests
{
    
    [Serializable]
    public class QuestBase : IQuest
    {

        public string ID { get; set; }

        public string Name { get; set; }

        public QuestStatus Status { get; set; } = QuestStatus.NotStarted;

        public QuestData Data { get; set; } = new QuestData();

        public IDictionary<int, string> StatusToDescription { get; set; } = new Dictionary<int, string>();

        public bool ContainsTag(string tag)
        {
            return Data.Tags.Contains(tag);
        }
        
        public bool NotContainsTag(string tag)
        {
            return !ContainsTag(tag);
        }

        public void AddTag(string tag)
        {
            Data.Tags.Add(tag);
        }

        public void RemoveTag(string tag)
        {
            Data.Tags.Remove(tag);
        }

    }
    
}
