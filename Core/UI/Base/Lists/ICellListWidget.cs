using Godot;

namespace Cthangover.Core.UI.Base.Lists
{
    
    public interface ICellListWidget<TItem, TModel> : IListWidget<TItem, TModel>
	    where TItem : class, IListItem<TModel>
    {
		
        Vector2 CellSize { get; }

        Vector2I ViewCellsCount { get; }
			
    }
    
}
