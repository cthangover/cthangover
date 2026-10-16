using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Events;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
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
    /// objects collected by <see cref="ModManager.GetResolver{EntityResolver}()"/> through the
    /// DTO's <c>[EntityKind("quest")]</c> attribute, the factory remains
    /// decoupled from any directory layout — new mods register quests simply
    /// by shipping a JSON file whose envelope declares the quest kind.
    /// <para/>
    /// The single instance is process-wide and deliberately not owned by
    /// <see cref="AppServices"/>: quest state is identity-sensitive across the
    /// whole process (save/restore, scenario conditions), and a swappable owner
    /// would break parallel test runs. The new-game reset is wired through the
    /// AppServices resettable adapter registered by QuestsModInitializer
    /// (see <see cref="ResetAll"/>).
    /// </summary>
    public class QuestFactory
    {

        /// <summary>
        /// Owns the process-wide instance; laziness defers construction to the
        /// first access, avoiding order-dependent initialisation races with
        /// other subsystems.
        /// </summary>
        private static Lazy<QuestFactory> instance = new(() => new QuestFactory());

    /// <summary>
    /// Process-wide singleton. Deliberately NOT owned by AppServices: quest
    /// state is identity-sensitive across the whole process (save/restore,
    /// scenario conditions), and a swappable owner breaks that contract in
    /// parallel test runs. Reset goes through the AppServices resettable
    /// adapter, construction timing stays lazy.
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

            if (!ModManager.Instance.IsInitialized)
            {
                GameLogger.Log("QUEST", "mods not initialized, quest loading deferred");
                return;
            }

            isLoaded = true;

            try
            {
                var definitions = Entities.Collect<QuestDefinition>();
                if (definitions != null)
                {
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
        /// properties. Entries whose ID is not registered (a removed mod or a
        /// quest renamed between sessions) are skipped with a warning so that
        /// an ageing save cannot block loading. Reads from empty or <c>null</c>
        /// lists are silently ignored so that save files created before the
        /// quest system existed (or with no quest progress) do not break the
        /// pipeline. Publishes a single <see cref="QuestStateRestoredEvent"/>
        /// marker after the restore loop (not per-quest) so condition caches
        /// invalidate once without a per-quest event storm.
        /// </summary>
        public void SetAll(List<QuestBase> quests)
        {
            EnsureLoaded();

            if(Lists.IsEmpty(quests))
                return;
            foreach (var questFromSave in quests)
            {
                if(!dataByID.TryGetValue(questFromSave.ID, out var quest))
                {
                    GameLogger.Log("QUEST", "quest '" + questFromSave.ID + "' from save is not registered, skipped", LogLevel.Warning);
                    continue;
                }
                quest.Data   = questFromSave.Data;
                quest.Status = questFromSave.Status;
            }

            ModEventBus.Publish(new QuestStateRestoredEvent());
        }

        /// <summary>
        /// Returns every quest to the state it has right after mod loading:
        /// <see cref="QuestStatus.NotStarted"/>, numeric stage zero, and an
        /// empty tag set. Called by <c>GameData.StartNewGame</c> because the
        /// factory is a process‑wide singleton that survives returning to the
        /// main menu — without this reset, tags and statuses earned in the
        /// previous playthrough would leak into a new game and satisfy (or
        /// block) prologue conditions that test for absent tags. Publishes
        /// <see cref="QuestStateRestoredEvent"/> so cached scenario conditions
        /// invalidate once.
        /// </summary>
        public void ResetAll()
        {
            EnsureLoaded();

            foreach (var quest in dataByID.Values)
            {
                quest.Status = QuestStatus.NotStarted;
                quest.Data = new QuestData();
            }

            ModEventBus.Publish(new QuestStateRestoredEvent());
        }

    }

}