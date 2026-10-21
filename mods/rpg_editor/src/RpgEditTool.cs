namespace Cthangover.Tools.RpgEditor
{
    /// <summary>Brush of the tile and collision layers.</summary>
    public enum RpgEditTool
    {
        /// <summary>Puts the selected tile (or collision code) under the pointer; dragging paints a stroke.</summary>
        Pencil = 0,

        /// <summary>Clears cells (collision back to auto).</summary>
        Eraser = 1,

        /// <summary>Flood-fills the connected area of equal cells.</summary>
        Fill = 2,
    }
}
