namespace Cthangover.Mods.Stats
{
    using System;
    using Cthangover.Core.Games;
    using Cthangover.Core.UI;
    using Cthangover.Mods.Stats.Formulas;
    using Godot;

    /// <summary>
    /// Exp and levels. Battles already add exp to the party roster; this class turns exp
    /// into levels by the character's exp curve, and lets scenarios give exp to anyone
    /// (<c>action stat.exp</c>) — a quest reward in an RPG, a lesson learned in a novel.
    /// <para/>
    /// The curve is the total exp needed to reach a level, a formula of <c>level</c>: the
    /// archetype's <see cref="ArchetypeInfo.ExpCurve"/>, else the game's setting
    /// <c>stats.exp_curve</c>, else <see cref="DefaultCurve"/> (20 for level 2, 60 for 3,
    /// 120 for 4…). Only characters with an archetype level up, unless the game sets
    /// <c>stats.leveling</c> to <c>all</c>: games that never heard of archetypes keep
    /// their levels exactly as before.
    /// </summary>
    public static class StatProgression
    {
        /// <summary>The curve used when neither the archetype nor the game names one.</summary>
        public const string DefaultCurve = "10 * level * (level - 1)";

        /// <summary>The level cap when the archetype names none.</summary>
        public const int DefaultMaxLevel = 99;

        /// <summary>Total exp needed to reach <paramref name="level"/> for <paramref name="who"/>.</summary>
        public static int ExpFor(string who, int level)
        {
            var archetype = StatsService.Archetype(StatsService.ArchetypeOf(who));
            var curve = archetype?.ExpCurve;
            if (string.IsNullOrWhiteSpace(curve))
                curve = ActiveGame.GetSetting("stats.exp_curve");
            if (string.IsNullOrWhiteSpace(curve))
                curve = DefaultCurve;
            return (int)Math.Max(0, Math.Round(Formula.Evaluate(curve, new LevelScope(level))));
        }

        /// <summary>The level cap of <paramref name="who"/>.</summary>
        public static int MaxLevel(string who)
        {
            var max = StatsService.Archetype(StatsService.ArchetypeOf(who))?.MaxLevel ?? 0;
            return max > 0 ? max : DefaultMaxLevel;
        }

        /// <summary>Whether <paramref name="who"/> gains levels from exp.</summary>
        public static bool Levels(string who)
            => !string.IsNullOrWhiteSpace(StatsService.ArchetypeOf(who))
               || string.Equals(ActiveGame.GetSetting("stats.leveling"), "all", StringComparison.OrdinalIgnoreCase);

        /// <summary>Adds exp (negative is ignored) and applies the level-ups it brings.</summary>
        public static void AddExp(string who, int amount)
        {
            var id = StatsService.Resolve(who);
            if (amount <= 0)
                return;
            var entry = StatsService.RosterEntry(id);
            if (entry != null)
                entry.Exp += amount;
            else
                StatState.Instance.Of(id).Exp += amount;
            ApplyLevelUps(id);
            StatsService.Changed(id);
        }

        /// <summary>
        /// Raises the level of <paramref name="characterId"/> while its exp reaches the
        /// next level, showing one notification per level; returns the levels gained.
        /// </summary>
        public static int ApplyLevelUps(string characterId)
        {
            if (!Levels(characterId))
                return 0;
            var gained = 0;
            var level = StatsService.LevelOf(characterId);
            var exp = StatsService.ExpOf(characterId);
            var max = MaxLevel(characterId);
            while (level < max && exp >= ExpFor(characterId, level + 1))
            {
                level++;
                gained++;
                SetLevel(characterId, level);
                Notify(characterId, level);
            }
            return gained;
        }

        private static void SetLevel(string characterId, int level)
        {
            var entry = StatsService.RosterEntry(characterId);
            if (entry != null)
                entry.Level = level;
            else
                StatState.Instance.Of(characterId).Level = level;
        }

        private static void Notify(string characterId, int level)
        {
            var nameKey = StatsService.Template(characterId)?.Name;
            var name = string.IsNullOrEmpty(nameKey) ? characterId : TranslationServer.Translate(nameKey).ToString();
            NotificationBridge.Show(string.Format(TranslationServer.Translate("stats/level_up").ToString(), name, level));
        }
    }
}
