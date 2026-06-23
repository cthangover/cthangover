using System;
using System.Collections.Generic;

namespace Cthangover.Core.Quests
{
    [Serializable]
    public class QuestData
    {
        public int Status { get; set; }
        public HashSet<string> Tags { get; set; } = new();

        public void SetStatus(int value)
        {
            Status = value;
        }
    }
}
