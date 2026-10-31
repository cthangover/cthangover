namespace Engine.Tests.Utils.PatchSamples
{
    using Cthangover.Core.Mods.Patches;

    /// <summary>Tile-shaped list element keyed by <see cref="Index"/> with a nested object (<see cref="Light"/>).</summary>
    public class PatchTileSample : IMergeable
    {
        string IMergeable.MergeKeyProperty => nameof(Index);

        /// <summary>Identity of the tile.</summary>
        public int Index { get; set; }

        /// <summary>Free text tag that a partial patch must keep.</summary>
        public string Tag { get; set; }

        /// <summary>Collision flag.</summary>
        public bool Solid { get; set; }

        /// <summary>Nested object merged field by field.</summary>
        public PatchLightSample Light { get; set; }
    }
}
