namespace Cthangover.Mods.Stats
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Cthangover.Core.Factories;

    /// <summary>
    /// What kind of person a character is (<c>"Kind": "archetype"</c>): an RPG class
    /// (warrior, mage), a novel's temperament (dreamer, cynic), a 3D game's background.
    /// It gives stat formulas per level, traits and the exp curve. A character names its
    /// archetype in its template (<c>"Archetype"</c>), and a scenario may change it
    /// (<c>action stat.archetype</c>), which is how a prologue lets the player choose.
    /// </summary>
    [EntityKind("archetype")]
    public class ArchetypeInfo : IIdentifiable
    {
        /// <summary>Archetype id.</summary>
        [JsonPropertyName("Id")]
        public string ID { get; set; }

        /// <summary>Locale key of the name.</summary>
        public string Name { get; set; }

        /// <summary>Locale key of the description.</summary>
        public string Description { get; set; }

        /// <summary>Optional picture in the <c>stats</c> file group (shown by the sheet and choice screens).</summary>
        public string Icon { get; set; }

        /// <summary>Stat id → value or formula of <c>level</c> and other stats (<c>"str": "6 + level * 2"</c>).</summary>
        public Dictionary<string, string> Stats { get; set; } = new();

        /// <summary>Modifiers every character of the archetype gets.</summary>
        public List<TraitInfo> Traits { get; set; } = new();

        /// <summary>
        /// Total exp needed to reach <c>level</c> (a formula of <c>level</c>); null takes the
        /// game's <c>stats.exp_curve</c> setting or the default curve.
        /// </summary>
        public string ExpCurve { get; set; }

        /// <summary>Highest level a character of the archetype reaches; 0 = the default 99.</summary>
        public int MaxLevel { get; set; }

        /// <summary>Battle actions learned on reaching a level (see <see cref="StatLearnings"/>).</summary>
        public List<ArchetypeLearning> Learnings { get; set; } = new();
    }
}
