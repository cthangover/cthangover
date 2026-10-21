using System;
using Cthangover.Core.Factories;

namespace Cthangover.Mods.Rpg
{
    /// <summary>Tilesets collected by the shared entity resolver (<c>"Kind": "rpg_tileset"</c>).</summary>
    public class RpgTilesetFactory : FileFactory<RpgTileset>
    {
        private static readonly Lazy<RpgTilesetFactory> instance = new(() => new RpgTilesetFactory());

        /// <summary>Shared instance.</summary>
        public static RpgTilesetFactory Instance => instance.Value;
    }
}
