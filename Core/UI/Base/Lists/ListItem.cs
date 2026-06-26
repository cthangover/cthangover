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

        public TModel Model { get; private set; }
        public Control Rect => this;

        public virtual void Construct(TModel model)
        {
            Model = model;
        }

        public abstract void Destruct();

    }

}
