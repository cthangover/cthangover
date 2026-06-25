using Cthangover.Core.Items;
using Cthangover.Core.Mods;

namespace Cthangover.Core.Actions
{
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