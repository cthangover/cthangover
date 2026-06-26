using Godot;

namespace Cthangover.Core.UI.Base.Lists
{
    /// <summary>
    /// Contract for a single item in a list widget. Construct binds a model;
    /// Destruct tears down (typically QueueFree). The Rect getter exposes the
    /// item's Control for layout positioning by the parent list.
    /// </summary>
    public interface IListItem<TModel>
    {
	
        Control Rect { get; }

        TModel Model { get; }

        void Construct(TModel model);

        void Destruct();
		
    }
    
}
