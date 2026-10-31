namespace Engine.Tests.Utils.PatchSamples
{
    using System.Collections.Generic;

    /// <summary>Tileset-shaped DTO: a list whose element type declares its key through <c>IMergeable</c>.</summary>
    public class PatchTilesetSample
    {
        /// <summary>Entity id.</summary>
        public string Id { get; set; }

        /// <summary>Tiles identified by <see cref="PatchTileSample.Index"/>.</summary>
        public List<PatchTileSample> Tiles { get; set; }
    }
}
