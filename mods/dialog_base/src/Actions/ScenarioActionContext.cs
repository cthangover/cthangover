using Cthangover.Core.Actions;
using Cthangover.Core.Items;
using Cthangover.Core.Mods;
using Cthangover.Core.Quests;
using Cthangover.Core.Settings;
using Cthangover.Mods.Dialog;
using Cthangover.Core.Utils;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Scenes;
using Cthangover.Core.Interactive;
using Cthangover.Mods.Map;
using Cthangover.Mods.Time;

namespace Cthangover.Mods.Dialog
{
    /// <summary>
    /// Concrete IActionContext wired to a DialogRuntime. Each service property
    /// is instantiated inline (no DI container) — this is intentional: scenario
    /// actions are short-lived commands, so creating new service instances per
    /// context is cheap and avoids shared state. GetParam delegates to the
    /// dialog runtime's variable store, bridging the dialog DSL's "set" variables
    /// to the atomic action system. The Inventory reference is try-caught because
    /// it may not exist at the moment of context creation (e.g. before game data
    /// is fully loaded).
    /// </summary>
    internal class ScenarioActionContext : IActionContext
    {
        private readonly DialogRuntime runtime;

        /// <summary>
        /// Service accessor for quest operations. Resolved from the kernel
        /// quest registry — the implementation (QuestServiceImpl) lives in
        /// the quests_base engine mod and is published there during mod
        /// load, so contexts created after boot always see a live service.
        /// </summary>
        public IQuestService     Quests      => QuestServiceRegistry.Provider;
        /// <summary>
        /// Service accessor for character party operations. Instantiated
        /// per-context. Routes to CharacterData via strict enum parsing.
        /// </summary>
        public ICharacterService Character   { get; } = new CharacterServiceImpl();
        /// <summary>
        /// Service accessor for lighting control. Instantiated per-context.
        /// Thin wrapper around UiLightController singleton.
        /// </summary>
        public ILightingService  Lighting    { get; } = new LightingServiceImpl();
        /// <summary>
        /// Service accessor for runtime scene node manipulation.
        /// Instantiated per-context. All operations route through
        /// SceneContextNode.Instance as the scene root.
        /// </summary>
        public ISceneNodeService Scene       { get; } = new SceneNodeServiceImpl();
        /// <summary>
        /// Singleton reference to the mod registry. Shared across all
        /// contexts because mod registration is global and mod data
        /// doesn't change during a single dialog execution.
        /// </summary>
        public IModRegistry      ModRegistry { get; } = Cthangover.Core.Mods.ModRegistry.Instance;
        /// <summary>
        /// Player inventory reference, captured once at context
        /// construction time. Try-caught because GameData may not be
        /// fully loaded when the context is created (e.g. during early
        /// dialog initialization before the game scene is ready).
        /// Consumers must tolerate a null inventory.
        /// </summary>
        public IInventory        Inventory   { get; }
        /// <summary>
        /// Map zone control. Stateless adapter over the
        /// <c>MapZoneService</c> singleton, which owns the actual state;
        /// safe to instantiate per-context like the quest service, because the
        /// state does not live in the adapter.
        /// </summary>
        public IMapZoneService   Map         { get; } = new MapZoneServiceImpl();

        /// <summary>
        /// Creates a context wired to the given DialogRuntime. The
        /// runtime provides the variable store for GetParam — without
        /// it, actions would have no way to receive parameters from the
        /// scenario DSL. Inventory is captured eagerly (not lazily)
        /// because it's fixed for the lifetime of this context.
        /// </summary>
        public ScenarioActionContext(DialogRuntime runtime)
        {
            this.runtime = runtime;
            try { Inventory = GameData.Instance?.Runtime?.Inventory; }
            catch { Inventory = null; }
        }

        /// <summary>
        /// Reads a named variable from the dialog runtime's variable
        /// store. This is the bridge between the scenario DSL's "set"
        /// command and the atomic action system — variables set in the
        /// script are consumed here. Returns null for undefined variables.
        /// </summary>
        public string GetParam(string name) => runtime.GetVariable(name);

        /// <summary>
        /// Emits a debug-level log entry via GameLogger. The category
        /// groups related entries for filtering; use consistent
        /// categories (e.g. "EVENT", "QUEST", "BATTLE") across actions.
        /// </summary>
        public void Log(string category, string message) => GameLogger.Log(category, message);

        /// <summary>
        /// Emits a warning-level log entry. Automatically prepends
        /// "[WARN]" to the message and logs at LogLevel.Warning.
        /// </summary>
        public void LogWarning(string category, string message) => GameLogger.Log(category, $"[WARN] {message}", LogLevel.Warning);
    }
}
