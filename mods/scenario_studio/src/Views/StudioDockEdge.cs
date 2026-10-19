namespace Cthangover.Mods.ScenarioStudio
{
    using System;

    /// <summary>
    /// Edges of the dock a <see cref="StudioDockDrag"/> handle moves: one edge
    /// or a corner resizes, all four at once (<see cref="Move"/>) shift the
    /// dock without changing its size.
    /// </summary>
    [Flags]
    public enum StudioDockEdge
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8,
        Move = Left | Top | Right | Bottom,
    }
}
