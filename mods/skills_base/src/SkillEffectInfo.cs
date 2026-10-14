using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Cthangover.Mods.Skills
{
    /// <summary>
    /// One numeric bonus granted by a skill card — the gameplay payload behind
    /// the collection. <see cref="Key"/> is the locale key naming the affected
    /// stat (e.g. <c>characters/skills/effects/recruit</c>), <see cref="Value"/>
    /// its magnitude and <see cref="Percent"/> the display unit.
    /// <para/>
    /// Effects sharing a key are summed by <see cref="SkillEffectAggregator"/>
    /// into the panel's global "collection effect" plate, so one key must keep
    /// the same unit across every skill: two effects of <c>+10%</c> and
    /// <c>+5%</c> become <c>+15%</c>, never <c>+15</c>. Mods author these
    /// values in the skill JSON next to the card's rarity and type.
    /// </summary>
    [Serializable]
    public class SkillEffectInfo
    {
        /// <summary>Locale key of the affected stat, shared by all skills that touch it.</summary>
        [JsonPropertyName("Key")]
        public string Key { get; set; }

        /// <summary>Magnitude in the unit encoded by <see cref="Percent"/>. Negative values are penalties.</summary>
        [JsonPropertyName("Value")]
        public float Value { get; set; }

        /// <summary>When true the value is displayed with a <c>%</c> suffix; still summed as a plain number.</summary>
        [JsonPropertyName("Percent")]
        public bool Percent { get; set; }

        /// <summary>
        /// Formats the magnitude for cards, chips and the collection plate. An
        /// explicit <c>+</c> marks positive values so a bonus never reads as a
        /// subtracted amount; trailing zeros are trimmed (<c>+50%</c>, not
        /// <c>+50.0%</c>); <c>%</c> is appended when <see cref="Percent"/> is
        /// set.
        /// </summary>
        public string FormatValue()
        {
            var number = Value.ToString("0.##", CultureInfo.InvariantCulture);
            if (Value > 0f)
                number = "+" + number;
            return Percent ? number + "%" : number;
        }
    }
}
