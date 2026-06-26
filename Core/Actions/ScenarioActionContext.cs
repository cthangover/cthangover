using Cthangover.Core.Items;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Actions
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

        public IQuestService     Quests      { get; } = new QuestServiceImpl();
        public ICharacterService Character   { get; } = new CharacterServiceImpl();
        public IBattleService    Battle      { get; } = new BattleServiceImpl();
        public ILightingService  Lighting    { get; } = new LightingServiceImpl();
        public ISceneNodeService Scene       { get; } = new SceneNodeServiceImpl();
        public IModRegistry      ModRegistry { get; } = Cthangover.Core.Mods.ModRegistry.Instance;
        public IInventory        Inventory   { get; }

        public ScenarioActionContext(DialogRuntime runtime)
        {
            this.runtime = runtime;
            try { Inventory = GameData.Instance?.Runtime?.Inventory; }
            catch { Inventory = null; }
        }

        public string GetParam(string name) => runtime.GetVariable(name);

        public void Log(string category, string message) => GameLogger.Log(category, message);
        public void LogWarning(string category, string message) => GameLogger.Log(category, $"[WARN] {message}", LogLevel.Warning);
    }
}
