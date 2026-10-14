using System.Collections.Generic;

namespace Cthangover.Core.Skills
{
    /// <summary>
    /// Mutable runtime container tracking which skills a character or the player
    /// currently owns. Maintains a set of lowercase string IDs (e.g. "slave_master",
    /// the canonical identifier format of the skill definitions owned by the
    /// skills_base engine mod). Owned skills are resolved to their definitions by
    /// the mod's SkillFactory when the UI needs to display the available skill pool.
    /// </summary>
    public class SkillData
    {
        /// <summary>
        /// The collection of owned skill IDs in lowercase string form (e.g. "slave_master").
        /// Exposed as <see cref="ISet{T}"/> to allow external read access while
        /// mutation is funneled through <see cref="AddSkill(string)"/> and
        /// <see cref="HasSkill(string)"/>.
        /// </summary>
        public ISet<string> Skills { get; private set; } = new HashSet<string>();

        /// <summary>
        /// Initialises an empty skill collection with no owned skills.
        /// </summary>
        public SkillData()
        { }

        /// <summary>
        /// Checks whether the given lowercase string skill ID is present in the
        /// owned skill set.
        /// </summary>
        /// <param name="id">The lowercase skill ID (e.g. "slave_master").</param>
        /// <returns><c>true</c> if the skill is owned; otherwise <c>false</c>.</returns>
        public bool HasSkill(string id)
        {
            return Skills.Contains(id);
        }

        /// <summary>
        /// Adds a skill to the owned set using its raw lowercase string ID.
        /// Duplicate additions are silently ignored.
        /// </summary>
        /// <param name="id">The lowercase skill ID (e.g. "slave_master").</param>
        public void AddSkill(string id)
        {
            Skills.Add(id);
        }

    }
}
