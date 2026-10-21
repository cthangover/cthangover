using System;
using Cthangover.Core.Factories;
using Cthangover.Core.Mods.Resolvers;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Maps collected by the shared entity resolver (<c>"Kind": "rpg_map"</c>).
    /// <see cref="Publish"/> lets the map editor put a just-saved map into the
    /// resolver's cache, so the running world shows the edit without a mod
    /// reload; the next reload reads the same content back from the file.
    /// </summary>
    public class RpgMapFactory : FileFactory<RpgMap>
    {
        private static readonly Lazy<RpgMapFactory> instance = new(() => new RpgMapFactory());

        /// <summary>Shared instance.</summary>
        public static RpgMapFactory Instance => instance.Value;

        /// <summary>Replaces the cached definition of <paramref name="map"/>'s id.</summary>
        public void Publish(RpgMap map)
        {
            if (map != null && !string.IsNullOrEmpty(map.ID))
                Entities.Collect<RpgMap>()[map.ID] = map;
        }
    }
}
