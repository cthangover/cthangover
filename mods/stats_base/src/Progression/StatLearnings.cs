namespace Cthangover.Mods.Stats
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Characters;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Settings;
    using Cthangover.Core.UI;
    using Godot;

    /// <summary>
    /// Teaches the battle actions of an archetype's <see cref="ArchetypeInfo.Learnings"/>
    /// when a party member reaches their level or takes the archetype. It works with the
    /// engine's own action model rather than beside it: a learned action goes into a free
    /// action slot of the member (three slots, as the character panel shows), else into the
    /// shared action pool, from where the player assigns it in the character panel — the
    /// same way a granted skill card arrives.
    /// <para/>
    /// Every action is taught once per character (remembered in the stats state), so
    /// switching archetypes back and forth never duplicates cards.
    /// </summary>
    public static class StatLearnings
    {
        /// <summary>Slots a party member has for actions (the character panel's three).</summary>
        public const int SlotCount = 3;

        /// <summary>Teaches what the archetype of <paramref name="characterId"/> offers up to its level; returns the new action ids.</summary>
        public static List<string> Apply(string characterId)
        {
            var taught = new List<string>();
            var entry = StatsService.RosterEntry(characterId);
            var archetype = StatsService.Archetype(StatsService.ArchetypeOf(characterId));
            if (entry == null || archetype?.Learnings == null)
                return taught;
            var level = StatsService.LevelOf(characterId);
            var state = StatState.Instance.Of(characterId);
            foreach (var learning in archetype.Learnings.Where(l => l != null && !string.IsNullOrWhiteSpace(l.Action) && l.Level <= level))
            {
                if (state.Learned.Contains(learning.Action, StringComparer.OrdinalIgnoreCase))
                    continue;
                state.Learned.Add(learning.Action);
                Place(entry, learning.Action);
                taught.Add(learning.Action);
                Notify(characterId, learning.Action);
            }
            return taught;
        }

        private static void Place(CharacterInfoData entry, string action)
        {
            entry.ActionSlots ??= new List<string>();
            if (entry.ActionSlots.Contains(action))
                return;
            var free = entry.ActionSlots.FindIndex(string.IsNullOrEmpty);
            if (free >= 0)
            {
                entry.ActionSlots[free] = action;
                return;
            }
            if (entry.ActionSlots.Count < SlotCount)
            {
                entry.ActionSlots.Add(action);
                return;
            }
            var pool = GameData.Instance?.Runtime?.ActionPool;
            if (pool != null && !pool.ActionIds.Contains(action))
                pool.ActionIds.Add(action);
        }

        private static void Notify(string characterId, string action)
        {
            var who = StatsService.Template(characterId)?.Name;
            var actionName = Entities.Collect<ActionCharacterInfo>().TryGetValue(action, out var info) ? info.Name : action;
            NotificationBridge.Show(string.Format(TranslationServer.Translate("stats/learned").ToString(),
                TranslationServer.Translate(who ?? characterId).ToString(), TranslationServer.Translate(actionName ?? action).ToString()));
        }
    }
}
