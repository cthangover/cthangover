namespace Cthangover.Mods.Stats
{
    using System.Text.Json.Serialization;
    using Cthangover.Core.Factories;

    /// <summary>
    /// One stat a game counts (<c>"Kind": "stat"</c>): strength, charisma, max health,
    /// sanity, reputation — the engine has no built-in list. A game or a pack defines its
    /// stats as data, so a visual novel can count "courage" and "trust" with the same
    /// machinery an RPG uses for "atk" and "def", and a mod can add a stat to someone
    /// else's game without code.
    /// <para/>
    /// A stat's value for a character is, in this order of precedence: the character's own
    /// <c>Stats</c> entry, its archetype's <c>Stats</c> entry, this stat's
    /// <see cref="Formula"/>, else <see cref="Default"/>; then permanent bonuses and
    /// traits are applied and the result is clamped to <see cref="Min"/>/<see cref="Max"/>.
    /// Formulas may read other stats by id and <c>level</c>, so derived stats
    /// (<c>"max_hp": "30 + vit * 6"</c>) need no code.
    /// </summary>
    [EntityKind("stat")]
    public class StatInfo : IIdentifiable
    {
        /// <summary>Stat id, lowercase; formulas, conditions and traits refer to it.</summary>
        [JsonPropertyName("Id")]
        public string ID { get; set; }

        /// <summary>Locale key of the full name ("Strength").</summary>
        public string Name { get; set; }

        /// <summary>Locale key of the short label in tables ("STR"); the name is used when empty.</summary>
        public string Short { get; set; }

        /// <summary>Locale key of the explanation shown in the character sheet and the editors.</summary>
        public string Description { get; set; }

        /// <summary>Optional picture in the <c>stats</c> file group.</summary>
        public string Icon { get; set; }

        /// <summary>Free group name the sheet groups rows by ("attributes", "combat", "social").</summary>
        public string Group { get; set; }

        /// <summary>Display order inside the sheet (lower first, then by id).</summary>
        public int Order { get; set; }

        /// <summary>Value used when nothing else defines the stat.</summary>
        public double Default { get; set; }

        /// <summary>Formula of the value when neither the character nor its archetype defines it.</summary>
        public string Formula { get; set; }

        /// <summary>Lower bound after bonuses and traits; null = none.</summary>
        public double? Min { get; set; }

        /// <summary>Upper bound after bonuses and traits; null = none.</summary>
        public double? Max { get; set; }

        /// <summary>
        /// True for a stat that is spent and refilled (mana, stamina): the sheet value is
        /// its maximum and the current amount is kept per character in the stats state.
        /// </summary>
        public bool Pool { get; set; }

        /// <summary>
        /// Name of the battle attribute the value is written to (<c>health</c>,
        /// <c>attack</c>, <c>defence</c>, <c>point</c>): the bridge that lets every battle
        /// core use the stats without knowing about them. Null = not bound.
        /// </summary>
        public string Binds { get; set; }

        /// <summary>Hidden stats are computed and usable in formulas but not listed in the sheet.</summary>
        public bool Hidden { get; set; }
    }
}
