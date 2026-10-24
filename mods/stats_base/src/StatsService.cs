namespace Cthangover.Mods.Stats
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Characters;
    using Cthangover.Core.Events;
    using Cthangover.Core.Games;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;

    /// <summary>
    /// The stats of every character in one place: computes and caches sheets, keeps what
    /// characters gained during play (<see cref="StatState"/>) and tells everyone when
    /// stats change. Everything else — scenario actions, conditions, checks, the sheet
    /// panel, equipment, battle formulas — goes through this facade, so there is one
    /// answer to "how strong is the hero now" in a novel, a tile RPG or any other game.
    /// <para/>
    /// <b>Who.</b> Every call takes a "who": a character id, or <c>player</c>. The player
    /// is the game's setting <c>stats.player</c>, else the first member of the battle set,
    /// else the free id <c>player</c> — a protagonist with a sheet but without a party
    /// card, which is what a visual novel needs.
    /// <para/>
    /// <b>Where numbers live.</b> Party members keep level and exp in the core roster
    /// (battles award exp there); others keep them in the stats state. Bound stats
    /// (<see cref="StatInfo.Binds"/>) are written into the roster's battle attributes by
    /// <see cref="StatSync"/>, so every battle core sees them without knowing this mod.
    /// </summary>
    public static class StatsService
    {
        /// <summary>The free id of a protagonist without a party card.</summary>
        public const string FreePlayerId = "player";

        private static readonly Dictionary<string, StatSheet> Sheets = new(StringComparer.OrdinalIgnoreCase);
        private static object snapshot;
        private static bool notifying;

        /// <summary>Stat definitions by id, sorted for display (order, then id).</summary>
        public static IReadOnlyList<StatInfo> Definitions =>
            Entities.Collect<StatInfo>().Values.Where(s => !string.IsNullOrWhiteSpace(s.ID))
                .OrderBy(s => s.Order).ThenBy(s => s.ID, StringComparer.Ordinal).ToList();

        /// <summary>The definition of <paramref name="stat"/>, or null.</summary>
        public static StatInfo Definition(string stat)
            => Find(Entities.Collect<StatInfo>(), stat);

        /// <summary>The archetype <paramref name="id"/>, or null.</summary>
        public static ArchetypeInfo Archetype(string id)
            => string.IsNullOrWhiteSpace(id) ? null : Find(Entities.Collect<ArchetypeInfo>(), id);

        /// <summary>The character template <paramref name="id"/>, or null.</summary>
        public static CharacterInfo Template(string id)
            => string.IsNullOrWhiteSpace(id) ? null : Find(Entities.Collect<CharacterInfo>(), id);

        /// <summary>
        /// Resolves a "who": <c>player</c> (or empty) → the player id; anything else is a
        /// character id taken as written.
        /// </summary>
        public static string Resolve(string who)
        {
            if (!string.IsNullOrWhiteSpace(who) && !string.Equals(who.Trim(), FreePlayerId, StringComparison.OrdinalIgnoreCase))
                return who.Trim();
            var setting = ActiveGame.GetSetting("stats.player");
            if (!string.IsNullOrWhiteSpace(setting))
                return setting.Trim();
            var roster = Roster;
            var first = roster?.Characters.Keys.FirstOrDefault(id => roster.BattleSet.Contains(id));
            return first ?? FreePlayerId;
        }

        /// <summary>
        /// The characters a scenario argument names: <c>party</c> → every roster member
        /// (the player alone when there is no roster), anything else → <see cref="Resolve"/>.
        /// </summary>
        public static List<string> Targets(string who)
        {
            if (string.Equals(who?.Trim(), "party", StringComparison.OrdinalIgnoreCase))
            {
                var members = Roster?.Characters.Keys.ToList() ?? new List<string>();
                return members.Count > 0 ? members : new List<string> { Resolve(null) };
            }
            return new List<string> { Resolve(who) };
        }

        /// <summary>The party roster of the running game, or null outside a game.</summary>
        public static CharacterData Roster => GameData.Instance?.Runtime?.CharacterData;

        /// <summary>The roster entry of <paramref name="characterId"/>, or null for a character outside the party.</summary>
        public static CharacterInfoData RosterEntry(string characterId)
        {
            var roster = Roster;
            if (roster == null || characterId == null)
                return null;
            return roster.Characters.TryGetValue(characterId, out var entry) ? entry : null;
        }

        /// <summary>The archetype id in effect: chosen during play, else the template's <c>"Archetype"</c>.</summary>
        public static string ArchetypeOf(string who)
        {
            var id = Resolve(who);
            var chosen = StatState.Instance.Find(id)?.Archetype;
            return !string.IsNullOrWhiteSpace(chosen) ? chosen : ExtraData.Text(Template(id)?.ExtraProperties, "Archetype");
        }

        /// <summary>Level: the roster's for party members, else the stats state's, else the template's, at least 1.</summary>
        public static int LevelOf(string who)
        {
            var id = Resolve(who);
            var entry = RosterEntry(id);
            if (entry != null)
                return Math.Max(1, entry.Level);
            var state = StatState.Instance.Find(id);
            if (state != null && state.Level > 0)
                return state.Level;
            return Math.Max(1, Template(id)?.Level ?? 1);
        }

        /// <summary>Exp: the roster's for party members, else the stats state's.</summary>
        public static int ExpOf(string who)
        {
            var id = Resolve(who);
            var entry = RosterEntry(id);
            return entry?.Exp ?? StatState.Instance.Find(id)?.Exp ?? 0;
        }

        /// <summary>
        /// Whether <paramref name="who"/> takes part in the stats: it has an archetype, its
        /// template carries <c>Stats</c> or <c>Traits</c>, it gained bonuses during play, or a
        /// trait source gives it something (worn gear). Only such characters get their battle
        /// attributes from the sheet, so loading a stats pack next to a game whose characters
        /// never heard of stats changes none of their numbers.
        /// </summary>
        public static bool Participates(string who)
        {
            var id = Resolve(who);
            if (!string.IsNullOrWhiteSpace(ArchetypeOf(id)))
                return true;
            var extra = Template(id)?.ExtraProperties;
            if (ExtraData.Find(extra, "Stats") != null || ExtraData.Find(extra, "Traits") != null)
                return true;
            if (StatState.Instance.Find(id)?.Bonus.Count > 0)
                return true;
            return TraitSources.Collect(id).Count > 0;
        }

        /// <summary>The current sheet of <paramref name="who"/> (cached until something changes).</summary>
        public static StatSheet Sheet(string who)
        {
            var id = Resolve(who);
            DropCacheOnNewData();
            if (Sheets.TryGetValue(id, out var cached))
                return cached;
            var sheet = StatCalculator.Calculate(id, Inputs(id));
            Sheets[id] = sheet;
            return sheet;
        }

        /// <summary>The inputs the sheet of <paramref name="characterId"/> is computed from (tools show them).</summary>
        public static StatInputs Inputs(string characterId)
        {
            var template = Template(characterId);
            var archetype = Archetype(ArchetypeOf(characterId));
            var traits = new List<TraitInfo>();
            if (archetype?.Traits != null)
                traits.AddRange(archetype.Traits.Where(t => t != null));
            traits.AddRange(ExtraData.Traits(template?.ExtraProperties, characterId));
            traits.AddRange(TraitSources.Collect(characterId));

            return new StatInputs
            {
                Level = LevelOf(characterId),
                Definitions = Entities.Collect<StatInfo>(),
                Own = ExtraData.Stats(template?.ExtraProperties, characterId),
                Archetype = archetype,
                Bonus = StatState.Instance.Find(characterId)?.Bonus ?? new Dictionary<string, double>(),
                Traits = traits,
            };
        }

        /// <summary>A permanent change of a stat (a strength potion, a story choice); negative values lower it.</summary>
        public static void AddBonus(string who, string stat, double value)
        {
            var id = Resolve(who);
            if (string.IsNullOrWhiteSpace(stat) || value == 0)
                return;
            var bonus = StatState.Instance.Of(id).Bonus;
            var key = stat.Trim().ToLowerInvariant();
            bonus[key] = (bonus.TryGetValue(key, out var old) ? old : 0) + value;
            Changed(id);
        }

        /// <summary>Changes the archetype of <paramref name="who"/> (null or empty returns to the template's).</summary>
        public static void SetArchetype(string who, string archetype)
        {
            var id = Resolve(who);
            StatState.Instance.Of(id).Archetype = string.IsNullOrWhiteSpace(archetype) ? null : archetype.Trim();
            Changed(id);
        }

        /// <summary>
        /// Tells everyone that stats of <paramref name="characterId"/> (null: of anyone) may
        /// have changed: drops cached sheets, writes bound stats into battle attributes,
        /// publishes <see cref="StatsChangedEvent"/> and asks conditions to re-evaluate.
        /// Re-entrant calls (a listener changing stats again) only drop the cache.
        /// </summary>
        public static void Changed(string characterId)
        {
            if (characterId == null)
                Sheets.Clear();
            else
                Sheets.Remove(characterId);
            if (notifying)
                return;
            notifying = true;
            try
            {
                StatSync.SyncRoster();
                ModEventBus.Publish(new StatsChangedEvent { CharacterId = characterId });
                ConditionStateMonitor.MarkStateChanged();
            }
            finally
            {
                notifying = false;
            }
        }

        /// <summary>Drops the cached sheet of <paramref name="characterId"/> without notifying anyone.</summary>
        public static void Forget(string characterId)
        {
            if (characterId != null)
                Sheets.Remove(characterId);
        }

        /// <summary>Drops every cached sheet (a new game, a load, a mod reload).</summary>
        public static void ClearCache()
        {
            Sheets.Clear();
            snapshot = null;
        }

        private static void DropCacheOnNewData()
        {
            // The entity resolver hands out the same dictionary until data changes (a reload,
            // an editor save), so its identity tells when every sheet is stale.
            var current = Entities.Collect<StatInfo>();
            if (ReferenceEquals(current, snapshot))
                return;
            snapshot = current;
            Sheets.Clear();
        }

        private static T Find<T>(Dictionary<string, T> all, string id) where T : class
        {
            if (all == null || id == null)
                return null;
            if (all.TryGetValue(id, out var hit))
                return hit;
            foreach (var pair in all)
            {
                if (string.Equals(pair.Key, id, StringComparison.OrdinalIgnoreCase))
                    return pair.Value;
            }
            return null;
        }
    }
}
