using Godot;

namespace Cthangover.Core.UI.Base.Lists
{

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
