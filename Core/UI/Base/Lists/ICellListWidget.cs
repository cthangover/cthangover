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
		
        Vector2 CellSize { get; }

        Vector2I ViewCellsCount { get; }
			
    }
    
}
