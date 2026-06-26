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
        string GetParam(string name);

        void Log(string category, string message);
        void LogWarning(string category, string message);

        IQuestService Quests { get; }
        ICharacterService Character { get; }
        IBattleService Battle { get; }
        ILightingService Lighting { get; }
        ISceneNodeService Scene { get; }
        IModRegistry ModRegistry { get; }
        IInventory Inventory { get; }
    }
}