using System.Collections.Generic;
using Cthangover.Core.Mods;

namespace Cthangover.Mods.Rpg
{
    /// <summary>Access to the merged map events of <see cref="RpgMapEventResolver"/>.</summary>
    public static class RpgMapEvents
    {
        /// <summary>The events of a map (an empty list when it has none); the list is shared, do not change it.</summary>
        public static IReadOnlyList<RpgMapEvent> Of(string map)
        {
            var maps = ModManager.Instance.GetResolver<RpgMapEventResolver>()?.Collect();
            return map != null && maps != null && maps.TryGetValue(map, out var list) ? list : new List<RpgMapEvent>();
        }

        /// <summary>Forgets the collected events, so the next <see cref="Of"/> reads the files again (after an editor save).</summary>
        public static void Invalidate() => ModManager.Instance.GetResolver<RpgMapEventResolver>()?.ClearCache();
    }
}
