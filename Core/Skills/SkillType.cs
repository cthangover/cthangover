namespace Cthangover.Core.Skills
{
    /// <summary>
    /// Classifies how a skill behaves during gameplay. This distinction drives
    /// which battle-phase the skill can be used in and how its effects are resolved
    /// by the combat engine.
    /// </summary>
    public enum SkillType
    {
        /// <summary>
        /// Must be intentionally triggered during the player's turn, consuming an action
        /// resource and targeting a specific ally or enemy.
        /// </summary>
        Active,
        /// <summary>
        /// Remains in effect continuously once acquired, providing a persistent buff
        /// or aura without requiring any player input.
        /// </summary>
        Passive,
        /// <summary>
        /// Applies directly to the owning character as a stat modifier or intrinsic trait,
        /// rather than being a card the player selects each turn.
        /// </summary>
        ForCharacter,
    }
}
