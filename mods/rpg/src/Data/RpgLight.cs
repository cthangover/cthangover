namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// A light an event gives off (a lamp, a fire, a lit window), drawn by
    /// <see cref="RpgLighting"/>. It brightens the cells around the event towards their
    /// own colours, so it shows at dusk and at night and is lost in daylight.
    /// </summary>
    public class RpgLight
    {
        /// <summary>Reach in cells from the event's centre; the light fades to nothing there.</summary>
        public float Radius { get; set; } = 3f;

        /// <summary>Colour (<c>#rrggbb</c>, optionally <c>#rrggbbaa</c>: alpha is the strength).</summary>
        public string Color { get; set; } = "#ffd8a0";
    }
}
