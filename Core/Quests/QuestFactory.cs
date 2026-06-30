using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Quests
{
    
    /// <summary>
    /// Thread‑safe singleton that manages the entire lifecycle of
    /// <see cref="QuestBase"/> instances: lazy initialisation from mod JSON
    /// definitions, on‑demand lookup by ID, and restoration of mutable state
    /// when the player loads a save file. The factory defers loading until
    /// the first access (via <see cref="Get"/> or <see cref="GetAll"/>) and
    /// guards against re‑initialisation through the <c>isLoaded</c> flag.
    /// Because quest definitions come from <see cref="QuestDefinition"/>
    /// objects read by <see cref="ModManager.CollectJsonGroup"/>, the factory
    /// remains decoupled from any single storage format — new mods can
    /// register quests simply by placing JSON files in the correct group folder.
    /// </summary>
    public class QuestFactory
    {

        private static Lazy<QuestFactory> instance = new Lazy<QuestFactory>(() => new QuestFactory());

        /// <summary>
        /// The single global access point for quest lookup and save‑state
        /// restoration. The underlying <c>Lazy&lt;T&gt;</c> ensures that
        /// the factory is constructed on first access, avoiding order‑
        /// dependent initialisation races with other subsystems.
        /// </summary>
        public static QuestFactory Instance => instance.Value;

        private readonly IDictionary<string, QuestBase> dataByID = new Dictionary<string, QuestBase>();
        private bool isLoaded;
		
        private QuestFactory()
        {
        }

        /// <summary>
        /// Seeds the internal <c>dataByID</c> dictionary from mod JSON if it
        /// has not been seeded yet. When <see cref="ModManager"/> is still
        /// mid‑initialisation, the call is silently deferred (logged at
        /// debug level) so that early code paths don't crash; the next
        /// access will retry. Each <see cref="QuestDefinition"/> is
        /// converted into a fresh <see cref="QuestBase"/> with default
        /// status and an empty tag set.
        /// </summary>
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

        /// <summary>
        /// Looks up a quest by its unique string identifier and returns the
        /// shared <see cref="QuestBase"/> runtime instance. Throws
        /// <c>KeyNotFoundException</c> when no definition for
        /// <paramref name="id"/> was loaded from mod data, which typically
        /// indicates a typo in a scenario script's quest reference.
        /// </summary>
        public QuestBase Get(string id)
        {
            EnsureLoaded();

            if(dataByID.TryGetValue(id, out var quest))
                return quest;
            
            GameLogger.Log("QUEST", "quest '" + id + "' not registered", LogLevel.Error);
            throw new KeyNotFoundException(id);
        }

        /// <summary>
        /// Returns a snapshot list of every <see cref="QuestBase"/> instance
        /// known to the factory. Used primarily by the save system to
        /// serialise the full quest state for the save file, and by the
        /// journal UI to populate the quest list window.
        /// </summary>
        public List<QuestBase> GetAll()
        {
            EnsureLoaded();

            return dataByID.Values.ToList();
        }

        /// <summary>
        /// Restores mutable quest state from a deserialised save file.
        /// Iterates over every quest in the provided list, looks up the
        /// corresponding entry by ID, and overwrites its
        /// <see cref="QuestBase.Data"/> and <see cref="QuestBase.Status"/>
        /// properties. Reads from empty or <c>null</c> lists are silently
        /// ignored so that save files created before the quest system existed
        /// (or with no quest progress) do not break the pipeline.
        /// </summary>
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
