using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Quests
{
    
    public class QuestFactory
    {

        private static Lazy<QuestFactory> instance = new Lazy<QuestFactory>(() => new QuestFactory());

        public static QuestFactory Instance => instance.Value;

        private readonly IDictionary<string, QuestBase> dataByID = new Dictionary<string, QuestBase>();
        private bool isLoaded;
		
        private QuestFactory()
        {
        }

        private void EnsureLoaded()
        {
            if (isLoaded)
                return;
            isLoaded = true;

            if (!ModManager.Instance.IsInitialized)
            {
                GameLogger.Log("QUEST", "mods not initialized, quest loading deferred");
                return;
            }

            try
            {
                var definitions = ModManager.Instance.CollectJsonGroup<QuestDefinition>("quests");
                foreach (var (id, def) in definitions)
                {
                    var quest = new QuestBase
                    {
                        ID = def.ID,
                        Name = def.Name,
                        StatusToDescription = def.StatusToDescription ?? new Dictionary<int, string>(),
                    };
                    dataByID.Add(quest.ID, quest);
                }
            }
            catch (Exception ex)
            {
                GameLogger.Log("QUEST", "failed to load quests from mods: " + ex.Message, LogLevel.Error);
            }

            GameLogger.Log("QUEST", "loaded '" + dataByID.Count + "' quests");
        }

        public QuestBase Get(string id)
        {
            EnsureLoaded();

            if(dataByID.TryGetValue(id, out var quest))
                return quest;
            
            GameLogger.Log("QUEST", "quest '" + id + "' not registered", LogLevel.Error);
            throw new KeyNotFoundException(id);
        }

        public List<QuestBase> GetAll()
        {
            EnsureLoaded();

            return dataByID.Values.ToList();
        }
        
        public void SetAll(List<QuestBase> quests)
        {
            EnsureLoaded();

            if(Lists.IsEmpty(quests))
                return;
            foreach (var questFromSave in quests)
            {
                var quest = Get(questFromSave.ID);
                quest.Data   = questFromSave.Data;
                quest.Status = questFromSave.Status;
            }
        }

    }

}
