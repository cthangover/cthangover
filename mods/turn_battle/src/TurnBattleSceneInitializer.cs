using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;

namespace Cthangover.TurnBattle
{
    /// <summary>
    /// Registers the turn-based battle view scene with the core so that
    /// <see cref="GodotSceneService"/> can route the logical "battle" scene to
    /// this mod's tscn. Runs at resources-ready — before any battle can be
    /// triggered — and is the reason the core no longer hardcodes a battle path.
    /// </summary>
    public class TurnBattleSceneInitializer : IModInitializer
    {
        private const string BattleScenePath = "scenes/battle/battle.tscn";

        /// <summary>No-op: registration only needs the resources-ready stage.</summary>
        public void OnModLoaded(string modId) { }

        /// <summary>Publishes the battle view scene path to <see cref="BattleSceneRegistry"/>.</summary>
        public void OnModResourcesReady()
        {
            BattleSceneRegistry.Register(BattleScenePath);
        }
    }
}
