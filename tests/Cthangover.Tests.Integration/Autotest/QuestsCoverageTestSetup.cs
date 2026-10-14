#if TOOLS
namespace Cthangover.Core.Autotest
{
    using System;
    using System.Collections;
    using System.Linq;
    using System.Reflection;
    using Cthangover.Core.Quests;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>
    /// Integration coverage for the quests subsystem migrated into the
    /// quests_base engine mod. The core keeps only the kernel contract
    /// (<c>IQuest</c> / <c>QuestStatus</c> plus
    /// <see cref="QuestServiceRegistry"/>); the factory and runtime state,
    /// the IQuestService implementation, the quest actions, the scene
    /// conditions with the QuestParser leaf parser and the journal UI live
    /// in the mod assembly, so every check here proves the core-to-mod
    /// wiring at engine runtime:
    /// <list type="bullet">
    /// <item>the mod initializer published the quest service provider into
    /// the core registry during <c>OnModLoaded</c>;</item>
    /// <item>migrated types (QuestFactory, QuestParser, the journal) are
    /// loaded from a mod assembly, and the core assembly no longer defines
    /// them;</item>
    /// <item>quest state is drivable end-to-end through the core contract:
    /// SetStatus/AddTag on the provider are visible to scenario conditions
    /// evaluated by the core (proving the mod registered the QuestParser and
    /// the EvaluationContext wiring works);</item>
    /// <item>the save round-trip (GetAll/SetAll) and the new-game reset
    /// behave as before the migration;</item>
    /// <item>the quest journal panel opens from the mod's own scene.</item>
    /// </list>
    /// The machine-readable line is parsed by <c>tests/Cthangover.TestRunner</c>.
    /// </summary>
    public partial class QuestsCoverageTestSetup : Node
    {
        /// <summary>Log category for all lines of this scene.</summary>
        private const string Category = "QUESTS_COVERAGE";

        /// <summary>Journal scene now shipped by the quests_base mod itself.</summary>
        private const string JournalScenePath = "res://mods/quests_base/scenes/ui/quest_journal.tscn";

        private int _passed;
        private int _failed;

        public override void _Ready()
        {
            // Mods are loaded by the boot pipeline (SceneManager autoload -> Boot.RunSync) before any scene _Ready,
            // so QuestsModInitializer.OnModLoaded has already run and published the provider.

            var kernelAssembly = typeof(GameData).Assembly;

            Check("quests mod publishes a quest service provider", QuestServiceRegistry.Provider != null);
            Check("quest service provider is registered from a mod assembly, not the core",
                QuestServiceRegistry.Provider != null
                && QuestServiceRegistry.Provider.GetType().Assembly != kernelAssembly);

            var questFactoryType = FindModType(kernelAssembly, "Cthangover.Core.Quests.QuestFactory");
            Check("migrated type QuestFactory is loaded from a mod assembly", questFactoryType != null);
            Check("migrated type QuestParser is loaded from a mod assembly",
                FindModType(kernelAssembly, "Cthangover.Core.Scenes.Parsing.Impls.QuestParser") != null);
            Check("core assembly no longer defines QuestFactory",
                kernelAssembly.GetType("Cthangover.Core.Quests.QuestFactory", throwOnError: false) == null);
            Check("core assembly no longer defines QuestJournalBehaviour",
                kernelAssembly.GetType("Cthangover.Core.UI.Quests.QuestJournalBehaviour", throwOnError: false) == null);

            // End-to-end state: seed a quest through the mod factory, drive it
            // through the core-side contract, read it back through conditions.
            var injected = InjectQuest(questFactoryType, "CoverageQ");
            Check("quest seeded through the mod factory is visible via the provider",
                injected && SafeBool(() => QuestServiceRegistry.Provider.Exists("CoverageQ")));

            SafeAction(() => QuestServiceRegistry.Provider.SetStatus("CoverageQ", "Progress"));
            Check("provider SetStatus flips the quest status",
                SafeBool(() => QuestServiceRegistry.Provider.Get("CoverageQ").Status == QuestStatus.Progress));
            Check("quest condition parses via the mod-registered QuestParser and evaluates",
                SafeBool(() => ScenarioCondition.Evaluate("CoverageQ.IsActive")));

            SafeAction(() => QuestServiceRegistry.Provider.AddTag("CoverageQ", "coverage_tag"));
            Check("provider AddTag satisfies the hasTag condition",
                SafeBool(() => ScenarioCondition.Evaluate("CoverageQ.hasTag(\"coverage_tag\")")));

            // Save round-trip through the factory (the quests_base saveable
            // serialises exactly this list into the mod slot).
            var snapshot = SafeObj(() =>
            {
                var instance = GetFactoryInstance(questFactoryType);
                return questFactoryType.GetMethod("GetAll").Invoke(instance, null);
            });
            SafeAction(() =>
            {
                var instance = GetFactoryInstance(questFactoryType);
                questFactoryType.GetMethod("SetAll").Invoke(instance, new[] { snapshot });
            });
            Check("save round-trip (GetAll/SetAll) keeps quest state",
                SafeBool(() =>
                {
                    var quest = QuestServiceRegistry.Provider.Get("CoverageQ");
                    return quest.Status == QuestStatus.Progress
                        && ScenarioCondition.Evaluate("CoverageQ.hasTag(\"coverage_tag\")");
                }));

            // The journal is a mod-owned panel: load its scene, put it into
            // the tree and confirm the widget comes up from mod sources.
            OpenJournal();

            // New-game reset returns the quest to its fresh state.
            SafeAction(() => questFactoryType.GetMethod("ResetAll").Invoke(GetFactoryInstance(questFactoryType), null));
            Check("new-game reset returns the quest to the fresh state",
                SafeBool(() =>
                {
                    var quest = QuestServiceRegistry.Provider.Get("CoverageQ");
                    return quest.Status == QuestStatus.NotStarted
                        && !ScenarioCondition.Evaluate("CoverageQ.hasTag(\"coverage_tag\")");
                }));

            GameLogger.Log(Category, $"[TEST_RESULT] name=coverage_quests passed={_passed} failed={_failed}");
            GetTree().Quit(_failed == 0 ? 0 : 1);
        }

        /// <summary>
        /// Opens the quest journal from the mod scene and verifies it comes up
        /// inside the tree. Mod scenes must go through the game's runtime tscn
        /// pipeline (ModScenePrefab → TscnScenes): native Godot loading cannot
        /// see mods/ (gdignore'd) and cannot bind runtime-compiled mod scripts
        /// — the SceneBuilder resolves behaviours through ScriptTypeRegistry.
        /// </summary>
        private void OpenJournal()
        {
            Node journal = null;
            try
            {
                journal = new ModScenePrefab(JournalScenePath).Instantiate();
                Check("quest journal instantiates from the quests_base mod", journal != null);

                var behaviourType = journal?.GetType();
                Check("journal behaviour resolves to the quests_base mod sources",
                    behaviourType != null
                    && behaviourType.Name == "QuestJournalBehaviour"
                    && behaviourType.Assembly != typeof(GameData).Assembly);

                if (journal != null)
                {
                    AddChild(journal);
                    Check("quest journal opens inside the tree", journal.IsInsideTree());

                    // The folio layout lives in quest_journal.tscn and the
                    // behaviour resolves it with hard GetNode paths in
                    // OnceConstruct — run the widget lifecycle so a broken
                    // scene<->code contract fails here.
                    var built = false;
                    try
                    {
                        journal.GetType().GetMethod("Show", Type.EmptyTypes)?.Invoke(journal, null);
                        built = journal.GetNodeOrNull("BookArea/Book/LeftPage/Content/Column/Scroll/QuestListContainer") != null
                            && journal.GetNodeOrNull("BookArea/Book/RightPage/Content/Scroll/Column/EntryText") != null;
                        journal.GetType().GetMethod("Hide", Type.EmptyTypes)?.Invoke(journal, null);
                    }
                    catch (Exception e)
                    {
                        var inner = e.InnerException ?? e;
                        GameLogger.Log(Category, "journal build threw: " + inner.Message
                            + "\n" + (inner.StackTrace?.Split('\n').FirstOrDefault() ?? ""), LogLevel.Error);
                    }
                    Check("quest journal scene builds (OnceConstruct resolves the folio)", built);
                }
            }
            catch (Exception e)
            {
                Fail("quest journal open threw: " + e.Message);
            }
            finally
            {
                journal?.QueueFree();
            }
        }

        /// <summary>Seeds a quest with the given id into the mod factory's internal dictionary via reflection.</summary>
        private bool InjectQuest(Type questFactoryType, string id)
        {
            try
            {
                var questBaseType = questFactoryType.Assembly.GetType("Cthangover.Core.Quests.QuestBase", throwOnError: true);
                var quest = Activator.CreateInstance(questBaseType);
                questBaseType.GetProperty("ID").SetValue(quest, id);
                questBaseType.GetProperty("Name").SetValue(quest, id);

                var instance = GetFactoryInstance(questFactoryType);
                var dataField = questFactoryType.GetField("dataByID", BindingFlags.Instance | BindingFlags.NonPublic);
                var dict = (IDictionary)dataField.GetValue(instance);
                dict[id] = quest;
                return true;
            }
            catch (Exception e)
            {
                GameLogger.Log(Category, "quest injection failed: " + e.Message, LogLevel.Error);
                return false;
            }
        }

        /// <summary>The process-wide factory singleton (public <c>Instance</c> facade on the mod type).</summary>
        private static object GetFactoryInstance(Type questFactoryType)
        {
            return questFactoryType.GetProperty("Instance").GetValue(null);
        }

        /// <summary>Finds a migrated type by full name in any non-kernel assembly.</summary>
        private static Type FindModType(Assembly kernelAssembly, string fullName)
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a != kernelAssembly && !a.IsDynamic)
                .SelectMany(a => SafeTypes(a))
                .FirstOrDefault(t => t.FullName == fullName);
        }

        private void Check(string what, bool ok)
        {
            if (ok)
            {
                _passed++;
                GameLogger.Log(Category, $"PASS {what}");
            }
            else
            {
                _failed++;
                GameLogger.Log(Category, $"FAIL {what}", LogLevel.Error);
            }
        }

        private void Fail(string what)
        {
            _failed++;
            GameLogger.Log(Category, $"FAIL {what}", LogLevel.Error);
        }

        private bool SafeBool(Func<bool> f)
        {
            try { return f(); }
            catch (Exception e) { GameLogger.Log(Category, "check failed: " + e.Message, LogLevel.Error); return false; }
        }

        private object SafeObj(Func<object> f)
        {
            try { return f(); }
            catch (Exception e) { GameLogger.Log(Category, "lookup failed: " + e.Message, LogLevel.Error); return null; }
        }

        private void SafeAction(Action a)
        {
            try { a(); }
            catch (Exception e) { GameLogger.Log(Category, "action failed: " + e.Message, LogLevel.Error); _failed++; }
        }

        private static Type[] SafeTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch { return Array.Empty<Type>(); }
        }
    }
}
#endif
