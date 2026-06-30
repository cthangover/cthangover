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
	
        /// <summary>The Control that this item occupies. Used by the parent list to position the item in layout.</summary>
        Control Rect { get; }

        /// <summary>The bound data model. Set via <see cref="Construct"/>.</summary>
        TModel Model { get; }

        /// <summary>Binds the item to a data model. Called by the list widget after instantiation, before layout positioning.</summary>
        void Construct(TModel model);

        /// <summary>Cleans up the item — typically disconnects signals and calls QueueFree. Called when the list is destroyed or refreshed.</summary>
        void Destruct();
		
    }
    
}
