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
        void Init(string sceneType, string enemies, string questId = null, string newTag = null);
    }
}