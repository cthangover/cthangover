using Godot;

namespace Cthangover.Core.UI.Base.Lists
{
    /// <summary>
    /// Extends IListWidget with grid/cell dimensions. CellSize drives item sizing;
    /// ViewCellsCount determines how many cells fit in the visible area (used for
    /// scroll/viewport calculations).
    /// </summary>
    public interface ICellListWidget<TItem, TModel> : IListWidget<TItem, TModel>
	    where TItem : class, IListItem<TModel>
    {
		
        /// <summary>Pixel dimensions of each cell in the grid. Drives item sizing; derived from content width and aspect ratio.</summary>
        Vector2 CellSize { get; }

        /// <summary>Number of cells visible in the viewport (columns × rows). Used for scroll range and visibility calculations.</summary>
        Vector2I ViewCellsCount { get; }
			
    }
    
}
