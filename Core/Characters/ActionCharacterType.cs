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

    /// <summary>
    /// Classification helper for <see cref="ActionCharacterType"/>: distinguishes
    /// the three in-battle targeting categories from the panel-level
    /// <see cref="ActionCharacterType.Passive"/> and
    /// <see cref="ActionCharacterType.Active"/> kinds, which never appear in a
    /// battle action menu.
    /// </summary>
    public static class ActionCharacterTypeExtension
    {
        /// <summary>Returns <c>true</c> for ToSelf/ToAlias/ToEnemy — the types battle targeting consumes.</summary>
        public static bool UseInBattle(this ActionCharacterType type)
        {
            return type == ActionCharacterType.ToSelf
                || type == ActionCharacterType.ToAlias
                || type == ActionCharacterType.ToEnemy;
        }
    }

}
