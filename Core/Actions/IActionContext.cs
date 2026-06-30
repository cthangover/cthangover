using Cthangover.Core.Items;
using Cthangover.Core.Mods;

namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Context passed to IScenarioAction.Run — provides dialog variable access
    /// (GetParam) and references to all subsystem service interfaces. Acts as a
    /// facade: a single object that grants the action access to quests, characters,
    /// battle, lighting, scene nodes, mod registry, and inventory, plus logging.
    /// GetParam reads from the DialogRuntime's variable store, so actions can
    /// consume values set by the scenario DSL's "set" command or by earlier actions.
    /// </summary>
    public interface IActionContext
    {
        /// <summary>
        /// Reads a named variable from the dialog runtime's variable store.
        /// Variables are populated by the scenario DSL's "set" command or by
        /// earlier actions during dialog execution. Returns null when the
        /// variable doesn't exist — callers must null-check. This is the sole
        /// data channel between scenario actions and the dialog system.
        /// </summary>
        string GetParam(string name);

        /// <summary>
        /// Emits a debug-level entry to the central game log. The category
        /// string groups related entries (convention: "EVENT", "QUEST",
        /// "BATTLE", "SCENE", "WIDGET"). Consistent categories enable
        /// log filtering during debugging of scenario scripts.
        /// </summary>
        void Log(string category, string message);

        /// <summary>
        /// Emits a warning-level entry to the central game log. The message
        /// is prefixed with "[WARN]" and logged at LogLevel.Warning. Use
        /// for non-fatal issues that scenario authors should investigate
        /// (missing variables, unregistered quests, etc.) without halting
        /// dialog execution.
        /// </summary>
        void LogWarning(string category, string message);

        /// <summary>
        /// Quest subsystem: full CRUD over quest state, status, tags, and UI
        /// notifications. Backed by QuestServiceImpl which uses QuestFactory
        /// for lookup and TryGet for safe fallback on missing quests.
        /// </summary>
        IQuestService Quests { get; }
        /// <summary>
        /// Character subsystem: add characters to the player's party and
        /// dispatch "character joined" UI notifications. Accepts character
        /// ID strings from scenario parameters.
        /// </summary>
        ICharacterService Character { get; }
        /// <summary>
        /// Battle subsystem: constructs BattleData from scenario parameters
        /// (scene type, enemy list, quest binding) and stores it in runtime
        /// data for consumption by the battle scene loader.
        /// </summary>
        IBattleService Battle { get; }
        /// <summary>
        /// Lighting subsystem: controls depth/albedo texture maps on the
        /// UiLightController singleton and toggles time-of-day lighting.
        /// Used for scene transitions with different lighting setups.
        /// </summary>
        ILightingService Lighting { get; }
        /// <summary>
        /// Scene node subsystem: runtime instantiation, removal, and typed
        /// lookup of nodes within the current scene tree. All operations go
        /// through SceneContextNode.Instance as the scene root.
        /// </summary>
        ISceneNodeService Scene { get; }
        /// <summary>
        /// Mod registry reference: allows scenario actions to query loaded
        /// mods, their metadata, and custom action registrations at runtime.
        /// Singleton instance shared across all contexts.
        /// </summary>
        IModRegistry ModRegistry { get; }
        /// <summary>
        /// Player inventory reference, obtained from GameData at context
        /// construction time. May be null if game data hasn't fully loaded
        /// yet — consumers must tolerate a null inventory.
        /// </summary>
        IInventory Inventory { get; }
    }
}