using System.Text.Json.Serialization;
using Cthangover.Core.Factories;

namespace Cthangover.Core.Cards.StatusEffect
{

    /// <summary>
    /// JSON-serialisable status effect definition loaded from mod files.
    /// The Actions field is an ID string resolved by StatusEffectActionFactory
    /// into an IStatusActions instance at runtime. Type uses
    /// JsonStringEnumConverter for human-readable config files.
    /// Implements IIdentifiable so it works with PrefabFactory caching.
    /// </summary>
    [System.Serializable]
    public class StatusEffectInfo : IIdentifiable
    {
        /// <summary>Unique effect identifier used as the factory key.
        /// Serialised as <c>"Id"</c> in JSON for consistency with the
        /// modding schema.</summary>
        [JsonPropertyName("Id")]
        public string ID { get; set; }
        /// <summary>Display name for the effect, shown in tooltips and the
        /// character status panel.</summary>
        public string Name { get; set; }
        /// <summary>Human-readable description explaining what the effect
        /// does.</summary>
        public string Description { get; set; }
        /// <summary>Category of the effect. Serialised as a string
        /// (<c>"Buff"</c>, <c>"Debuff"</c>, <c>"Stun"</c>) via
        /// <c>JsonStringEnumConverter</c> for readable mod configs.</summary>
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public StatusEffectType Type { get; set; }
        /// <summary>Base duration in turns before the effect naturally
        /// expires.</summary>
        public int Duration { get; set; }
        /// <summary>Factory ID string for the behaviour set, resolved by
        /// <c>StatusEffectActionFactory</c> into an
        /// <see cref="IStatusActions"/> instance at runtime.</summary>
        public string Actions { get; set; }
        /// <summary>Filename of the icon texture within the
        /// <c>"characters"</c> mod group, loaded by
        /// <see cref="StatusEffectItem"/> during construction.</summary>
        public string Icon { get; set; }
    }
}
