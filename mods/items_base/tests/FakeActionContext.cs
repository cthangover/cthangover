namespace Cthangover.Mods.Items.Tests
{
    using System.Collections.Generic;
    using Cthangover.Core.Actions;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods;

    /// <summary>Action context for scenario action tests: dialog variables and an inventory, no other services.</summary>
    public class FakeActionContext : IActionContext
    {
        /// <summary>Dialog variables returned by <see cref="GetParam"/>.</summary>
        public Dictionary<string, string> Params { get; } = new();

        /// <summary>Warnings the action logged.</summary>
        public List<string> Warnings { get; } = new();

        public IQuestService Quests => null;
        public ICharacterService Character => null;
        public ILightingService Lighting => null;
        public ISceneNodeService Scene => null;
        public IModRegistry ModRegistry => null;
        public IInventory Inventory { get; set; }
        public IMapZoneService Map => null;

        public string GetParam(string name) => Params.TryGetValue(name, out var value) ? value : null;

        public void Log(string category, string message)
        {
        }

        public void LogWarning(string category, string message) => Warnings.Add(message);
    }
}
