using Cthangover.Core.Actions;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Mods;
using Cthangover.Core.Quests;
using Cthangover.Core.Scenes.Parsing;
using Cthangover.Core.Scenes.Parsing.Impls;
using Cthangover.Core.Utils;

namespace Cthangover.Mods.Quests
{
    /// <summary>
    /// Engine-mod initializer for the quests subsystem. Publishes the services
    /// living in this assembly to the kernel contracts as soon as the assembly
    /// is registered, so core-side consumers — dialog action contexts
    /// (<c>ctx.Quests</c>) and scenario condition evaluation — work through the
    /// core registry without referencing a runtime-compiled mod assembly.
    /// <para/>
    /// Three publications happen here:
    /// <list type="bullet">
    /// <item><see cref="QuestServiceRegistry.Provider"/> — the
    /// <see cref="IQuestService"/> implementation used by
    /// <c>ScenarioActionContext</c> and <c>GDQuestService</c>;</item>
    /// <item><see cref="LeafParserRegistry.RegisterFirst"/> — the quest leaf
    /// parser for scene conditions (<c>QuestId.method(...)</c> /
    /// <c>QuestId.Property op n</c>); the parser moved out of the core
    /// registry so that the parser set is fully extensible;</item>
    /// <item>the new-game resettable (id <c>quests</c>) so
    /// <c>GameData.StartNewGame</c> clears quest state through the common
    /// AppServices loop (the core used to register this lambda itself).</item>
    /// </list>
    /// Registration happens in <see cref="OnModLoaded"/> (the earliest and most
    /// frequent initializer callback) rather than on resources-ready, so any
    /// mod or scenario evaluated later already resolves quests. Registration is
    /// guarded and idempotent because <c>ModManager.Reload()</c> re-runs
    /// <see cref="OnModLoaded"/> for every mod — the guard prevents duplicate
    /// parsers and provider overwrites; the resettable registration is
    /// deduplicated by AppServices itself.
    /// </summary>
    public class QuestsModInitializer : IModInitializer
    {
        private static bool registered;

        /// <summary>Publishes the service provider, leaf parser and reset owner; safe to call again after a mod reload.</summary>
        public void OnModLoaded(string modId)
        {
            if (registered)
                return;
            registered = true;

            QuestServiceRegistry.Provider = new QuestServiceImpl();
            LeafParserRegistry.RegisterFirst(new QuestParser());

            GameLogger.Log("MODS", "QuestsModInitializer: quest service provider and QuestParser registered");
        }

        /// <summary>Registers the new-game quest reset owner with AppServices (deduplicated by id).</summary>
        public void OnModResourcesReady()
        {
            AppServices.Current.RegisterResettable(new QuestsResettable());
        }
    }

    /// <summary>
    /// New-game state owner of the quests mod: returns every quest to its
    /// fresh state (<see cref="QuestFactory.ResetAll"/>). Registered from
    /// <see cref="QuestsModInitializer.OnModResourcesReady"/>; AppServices
    /// deduplicates by <see cref="Id"/>, so repeated registration is a no-op.
    /// </summary>
    internal sealed class QuestsResettable : IResettable
    {
        /// <summary>Resettable key under which AppServices deduplicates registration.</summary>
        public string Id => "quests";

        /// <summary>Resets all quest statuses, stages and tags on "New Game".</summary>
        public void ResetForNewGame() => QuestFactory.Instance.ResetAll();
    }
}
