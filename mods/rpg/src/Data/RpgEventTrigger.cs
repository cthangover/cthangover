using System.Text.Json.Serialization;

namespace Cthangover.Mods.Rpg
{
    /// <summary>How the player sets off an <see cref="RpgMapEvent"/>.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RpgEventTrigger
    {
        /// <summary>The action key (E or F) while facing the event, or standing on it.</summary>
        Action = 0,

        /// <summary>Stepping onto the event's cell (doors, map exits, traps).</summary>
        Touch = 1,
    }
}
