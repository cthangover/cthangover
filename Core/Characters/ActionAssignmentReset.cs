using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Settings;

namespace Cthangover.Core.Characters
{
    /// <summary>
    /// Implements the character panel's "reset assignments" operation: every
    /// party member's action slots go back to the first three actions of their
    /// factory template, and template actions beyond the third are pushed into
    /// the shared action pool.
    /// <para/>
    /// The critical invariant is that awarded cards survive the reset. Cards
    /// can enter the pool from sources that have no template entry at all —
    /// a skill granted through <see cref="Skills.SkillService"/> being the
    /// canonical example — and once such a card is assigned to a slot it is
    /// absent from the pool. Rebuilding the slots without first returning the
    /// old slot contents to the pool would silently destroy it. Therefore all
    /// non-empty slot IDs are collected before the slots are overwritten;
    /// template IDs are filtered back out afterwards, so the pool ends up
    /// holding exactly the awarded cards that are no longer assigned.
    /// <para/>
    /// The template lookup is injected as a delegate because the production
    /// caller resolves it through <c>CharacterFactory</c> (a mod-aware JSON
    /// factory), while tests can supply plain <see cref="Character"/> objects.
    /// </summary>
    public static class ActionAssignmentReset
    {
        /// <summary>
        /// Rewrites every battle-set character's <see cref="CharacterInfoData.ActionSlots"/>
        /// from its template and replaces <see cref="ActionPoolData.ActionIds"/>
        /// with the surviving pool plus recovered awarded cards.
        /// </summary>
        /// <param name="characterData">Roster whose battle set and slots are reset.</param>
        /// <param name="actionPool">Shared action pool; may be <c>null</c>, in which case pool contents are recomputed but not stored.</param>
        /// <param name="resolveTemplate">Resolves a character ID to its factory template; may return <c>null</c> for unknown IDs.</param>
        public static void Apply(
            CharacterData characterData,
            ActionPoolData actionPool,
            Func<string, Character> resolveTemplate)
        {
            if (characterData?.BattleSet == null || characterData.Characters == null)
                return;

            var extraPoolIds = new HashSet<string>(actionPool?.ActionIds ?? new List<string>());
            var assignedAfterReset = new HashSet<string>();

            foreach (var characterId in characterData.BattleSet)
            {
                if (!characterData.Characters.TryGetValue(characterId, out var info))
                    continue;

                if (info.ActionSlots != null)
                    foreach (var slotId in info.ActionSlots)
                        if (!string.IsNullOrEmpty(slotId))
                            extraPoolIds.Add(slotId);

                var defaultActionIds = resolveTemplate?.Invoke(characterId)?.Actions?
                    .Select(action => action.ID).ToList() ?? new List<string>();

                var slots = new List<string>();
                for (var i = 0; i < 3; i++)
                {
                    if (i < defaultActionIds.Count)
                    {
                        slots.Add(defaultActionIds[i]);
                        assignedAfterReset.Add(defaultActionIds[i]);
                    }
                    else
                    {
                        slots.Add(null);
                    }
                }

                for (var i = 3; i < defaultActionIds.Count; i++)
                    extraPoolIds.Add(defaultActionIds[i]);

                info.ActionSlots = slots;
            }

            extraPoolIds.ExceptWith(assignedAfterReset);
            if (actionPool != null)
                actionPool.ActionIds = extraPoolIds.ToList();
        }
    }
}
