using System.Text.Json.Serialization;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Facing of an actor on the grid. The order is the row order of an RPG Maker
    /// style charset (down, left, right, up), so the value is also the frame row
    /// of <see cref="RpgActor"/>. JSON spells it as a word ("down", "Left").
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum RpgDirection
    {
        Down = 0,
        Left = 1,
        Right = 2,
        Up = 3,
    }
}
