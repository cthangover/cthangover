using System.Collections.Generic;

namespace Cthangover.Mods.Skills
{
    /// <summary>
    /// Sums skill effects across a set of owned skills into the totals shown by
    /// the skills panel's global "collection effect" plate. The result is what
    /// the player actually gains right now — a card collected from any mod
    /// changes it immediately, without touching the individual definitions.
    /// <para/>
    /// Aggregation is keyed by <see cref="SkillEffectInfo.Key"/> and preserves
    /// first-seen order, so the plate does not reshuffle between openings and a
    /// newly granted skill appends its unique effects at the end. Null skills,
    /// null effect lists and effects without a key are skipped: mod JSON is not
    /// trusted to be complete.
    /// </summary>
    public static class SkillEffectAggregator
    {
        /// <summary>
        /// Builds the collection-wide totals from the given skills. Effects with
        /// the same key are summed; the <c>Percent</c> flag survives if any
        /// contributor set it, so a mixed authoring mistake degrades to the
        /// percent display instead of silently dropping the suffix.
        /// </summary>
        /// <param name="skills">Owned skills, usually resolved from <see cref="SkillData.Skills"/>.</param>
        /// <returns>One entry per unique effect key, in first-seen order. Empty when nothing contributes.</returns>
        public static List<SkillEffectInfo> Aggregate(IEnumerable<SkillInfo> skills)
        {
            var order = new List<string>();
            var totals = new Dictionary<string, SkillEffectInfo>();

            if (skills != null)
            {
                foreach (var skill in skills)
                {
                    if (skill?.Effects == null)
                        continue;

                    foreach (var effect in skill.Effects)
                    {
                        if (effect == null || string.IsNullOrEmpty(effect.Key))
                            continue;

                        if (!totals.TryGetValue(effect.Key, out var total))
                        {
                            total = new SkillEffectInfo { Key = effect.Key, Percent = effect.Percent };
                            totals[effect.Key] = total;
                            order.Add(effect.Key);
                        }

                        total.Value += effect.Value;
                        total.Percent |= effect.Percent;
                    }
                }
            }

            var result = new List<SkillEffectInfo>();
            foreach (var key in order)
                result.Add(totals[key]);
            return result;
        }
    }
}
