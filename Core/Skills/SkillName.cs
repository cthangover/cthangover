namespace Cthangover.Core.Skills
{
    /// <summary>
    /// Enumerates every skill available in the game by its canonical identifier.
    /// Each member corresponds to a JSON-definition entry loaded by
    /// <see cref="SkillFactory"/> and used to construct <see cref="SkillInfo"/> instances.
    /// The underlying <c>int</c> values are not manually assigned; the enum serves
    /// purely as a compile-time-safe skill reference.
    /// </summary>
    public enum SkillName : int
    {
        /// <summary>
        /// A domination-type skill that allows the player to command enslaved units,
        /// overriding enemy AI and redirecting attacks under the player's control.
        /// </summary>
        SLAVE_MASTER
    }

    /// <summary>
    /// Provides conversion helpers for <see cref="SkillName"/> values, used when
    /// bridging the enum-based API with string-based lookups in
    /// <see cref="SkillData"/> and factory registries.
    /// </summary>
    public static class SkillNameExtensions
    {
        /// <summary>
        /// Converts a <see cref="SkillName"/> enum value to its lowercase string
        /// identifier, matching the format expected by <see cref="SkillData.Skills"/>
        /// and serialization layers.
        /// </summary>
        /// <param name="skill">The skill enum value to convert.</param>
        /// <returns>The lowercase name of the enum constant (e.g. "slave_master").</returns>
        public static string ID(this SkillName skill)
        {
            return skill.ToString().ToLower();
        }
    }
}
