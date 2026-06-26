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
        [JsonPropertyName("Id")]
        public string ID { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public StatusEffectType Type { get; set; }
        public int Duration { get; set; }
        public string Actions { get; set; }
        public string Icon { get; set; }
    }
}
