using Cthangover.Core.Mods;

namespace Mods.ModManager
{
    /// <summary>
    /// One dependency line of the scheme: the two chips it connects plus the
    /// flags that decide its paint style. Edges are rebuilt on every canvas
    /// rebuild and drawn by <see cref="CollectionEdgeLayer"/> from the chips'
    /// live global rectangles, so dragging a chip or a whole group keeps the
    /// lines attached without recomputing them.
    /// <para/>
    /// <see cref="Cross"/> is the "dangerous layout" flag: the dependency's two
    /// mods live in different mechanics, which means the player separated what
    /// belongs together. Cross edges are painted amber (red when the selection
    /// touches them) so the mistake is visible across the whole canvas.
    /// </summary>
    public class CollectionEdge
    {
        /// <summary>Mod id of the dependency (the side the arrow starts at).</summary>
        public string FromModId { get; set; }

        /// <summary>Mod id of the dependent mod.</summary>
        public string ToModId { get; set; }

        /// <summary>True when the two mods belong to different groups.</summary>
        public bool Cross { get; set; }

        /// <summary>True when the selection touches one of the endpoints.</summary>
        public bool Active { get; set; }
    }
}
