namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Battle initiation contract for scenario actions. Init() constructs a
    /// BattleData from scene background, enemy list, and optional quest binding.
    /// The sceneType parameter maps to Godot scene types; enemies is a
    /// comma-separated list resolved by BattleData.InitBattle.
    /// </summary>
    public interface IBattleService
    {
        /// <summary>
        /// Constructs and stores BattleData for the upcoming battle encounter.
        /// Captures the current scene background texture (via
        /// BackgroundFactory using SceneContextNode.LastBackgroundID) and
        /// depth/albedo lighting maps from UiLightController to preserve
        /// visual context. The enemies string is a comma-separated list
        /// resolved by BattleData.InitBattle. If questId is non-null, binds
        /// the quest to the battle and optionally dispatches a notification
        /// for newTag. Active battle core is resolved from BattleCoreRegistry
        /// — wrapped in try/catch because the registry may be uninitialized
        /// outside of battle contexts. The resulting BattleData is stored in
        /// GameData.Instance.Runtime.BattleData for the battle loader.
        /// </summary>
        void Init(string sceneType, string enemies, string questId = null, string newTag = null);
    }
}