using Godot;

namespace Cthangover.Core.UI.Base.Lists
{
    /// <summary>
    /// Base list item: stores the bound model and provides a Rect for layout.
    /// The virtual Construct saves the model reference; Destruct is abstract so
    /// subclasses must explicitly implement cleanup (signal disconnection, QueueFree).
    /// </summary>
    public abstract partial class ListItem<TModel> : Control, IListItem<TModel>
    {

        /// <summary>The data model bound to this list item. Set by <see cref="Construct"/>.</summary>
        public TModel Model { get; private set; }
        /// <summary>The Control that this item occupies for layout. Returns itself — ListItem is its own layout root.</summary>
        public Control Rect => this;

        /// <summary>Saves the model reference. Override in subclasses to apply model data to UI elements.</summary>
        public virtual void Construct(TModel model)
        {
            Model = model;
        }

        /// <summary>Required cleanup method. Subclasses must implement — typically disconnects signals and calls QueueFree.</summary>
        public abstract void Destruct();

    }

}
