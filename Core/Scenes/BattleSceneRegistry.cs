using Cthangover.Core.Utils;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Self-registration point for the battle view scene. The battle GUI lives in
    /// a mod (<c>turn_battle</c>), so the core cannot hardcode its path; the mod's
    /// initializer calls <see cref="Register"/> with a mod-relative scene path and
    /// <see cref="GodotSceneService"/> routes the logical "battle" scene there,
    /// switching via the runtime tscn pipeline instead of the native loader.
    /// Cleared on mod reload together with the other UI registries.
    /// </summary>
    public static class BattleSceneRegistry
    {
        private static string scenePath;

        /// <summary>Mod-relative path of the battle view scene, or null when no provider registered.</summary>
        public static string ScenePath => scenePath;

        /// <summary>
        /// Registers the battle view scene. Called once by the mod that owns the
        /// battle GUI; a later registration overrides an earlier one (mod load order).
        /// </summary>
        public static void Register(string modRelativeScenePath)
        {
            if (string.IsNullOrWhiteSpace(modRelativeScenePath))
            {
                GameLogger.Log("BATTLE", "BattleSceneRegistry.Register: empty scene path ignored", LogLevel.Error);
                return;
            }

            scenePath = modRelativeScenePath;
            GameLogger.Log("BATTLE", $"BattleSceneRegistry: battle view scene = '{modRelativeScenePath}'");
        }

        /// <summary>Drops the registration; next battle route fails until a mod re-registers.</summary>
        public static void Clear()
        {
            scenePath = null;
        }
    }
}
