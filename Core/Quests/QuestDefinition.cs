using System.Collections.Generic;
using Cthangover.Core.Factories;

namespace Cthangover.Core.Quests
{
    public class QuestDefinition : IIdentifiable
    {
        public string ID { get; set; }
        public string Name { get; set; }
        public Dictionary<int, string> StatusToDescription { get; set; }
    }
}
