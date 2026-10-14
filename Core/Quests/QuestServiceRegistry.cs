using Cthangover.Core.Actions;

namespace Cthangover.Core.Quests
{
    /// <summary>
    /// Kernel access point for quest operations, decoupling the core from the
    /// engine mod that owns quest state (<c>mods/quests_base</c>). The mod's
    /// initializer assigns <see cref="Provider"/> during
    /// <c>IModInitializer.OnModLoaded</c>; core-side consumers —
    /// <c>ScenarioActionContext</c>, <c>GDQuestService</c> and the scenario
    /// condition evaluation context — resolve quests through it. Before the
    /// mod registers (tools, very early boot) <see cref="Provider"/> is
    /// <c>null</c>: action contexts are created only after the boot pipeline
    /// has loaded mods, and unit tests inject their own
    /// <see cref="IQuestService"/> implementations.
    /// </summary>
    public static class QuestServiceRegistry
    {
        /// <summary>Provider published by the quests engine mod; <c>null</c> until the mod loads.</summary>
        public static IQuestService Provider { get; set; }
    }
}
