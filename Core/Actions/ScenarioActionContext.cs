using Cthangover.Core.Items;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Actions
{
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
