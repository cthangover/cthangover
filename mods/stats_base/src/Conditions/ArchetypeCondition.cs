namespace Cthangover.Mods.Stats
{
    using System;
    using Cthangover.Core.Scenes;

    /// <summary><c>archetype(who, id)</c>: true when the character's archetype is <see cref="Archetype"/>.</summary>
    public class ArchetypeCondition : ConditionNode
    {
        /// <summary>Creates the condition.</summary>
        public ArchetypeCondition(string who, string archetype)
        {
            Who = who;
            Archetype = archetype;
        }

        /// <summary>Whose archetype.</summary>
        public string Who { get; }

        /// <summary>The archetype id it must be.</summary>
        public string Archetype { get; }

        /// <inheritdoc />
        public override bool Evaluate(EvaluationContext ctx)
            => string.Equals(StatsService.ArchetypeOf(Who), Archetype, StringComparison.OrdinalIgnoreCase);
    }
}
