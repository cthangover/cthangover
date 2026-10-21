using Cthangover.Core.Characters;
using Cthangover.Core.Settings;

namespace Cthangover.Core.Relationship
{
    /// <summary>
    /// Gating predicate for the recruit system. Implementations are
    /// auto-discovered from mod assemblies by
    /// <see cref="RecruitBehaviourRegistry.RegisterAssembly"/> and
    /// evaluated conjunctively — <b>all</b> conditions must return
    /// <c>true</c> for recruitment to succeed.
    ///
    /// Conditions receive the full <see cref="Character"/> and
    /// <see cref="RuntimeData"/> so they can inspect character stats,
    /// inventory, relationship flags, quest state, or any other
    /// runtime-accessible data when deciding whether a given enemy is
    /// recruitable.
    /// </summary>
    public interface IRecruitCondition
    {
        /// <summary>Unique identifier for debugging and override lookups.</summary>
        string Id { get; }

        /// <summary>
        /// Returns <c>true</c> if the given <paramref name="enemy"/>
        /// can be recruited under current game state. Called from
        /// <c>RecruitBehaviourRegistry.CanRecruit</c> as part of a
        /// conjunctive gate — a single <c>false</c> blocks recruitment.
        /// </summary>
        bool CanRecruit(Character enemy, RuntimeData runtime);
    }

}
