namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Target category for a battle action. ToSelf affects the caster, ToAlias
    /// targets allies, ToEnemy targets opponents. This simple tri-state covers
    /// the targeting logic without complex targeting rules — the battle system
    /// uses this to filter valid targets when the player selects an action.
    /// </summary>
    public enum ActionCharacterType
    {
        ToSelf,
        ToAlias,
        ToEnemy,
        Passive,
        Active,
    };

    public static class ActionCharacterTypeExtension
    {
        public static bool UseInBattle(this ActionCharacterType type)
        {
            return type == ActionCharacterType.ToSelf
                || type == ActionCharacterType.ToAlias
                || type == ActionCharacterType.ToEnemy;
        }
    }

}
