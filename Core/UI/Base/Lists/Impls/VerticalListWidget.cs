using Godot;

namespace Cthangover.Core.UI.Base.Lists.Impls
{

    public abstract partial class VerticalListWidget<TItem, TModel> : ListWidget<TItem, TModel>
        where TItem : Control, IListItem<TModel>
    {

        protected override void PutToLayout(TItem item, int index, Control container, Vector2 contentSize)
        {
            var posY = index * (PrefabRect.Size.Y + ItemsCellPadding.Y);
            var itemRect = item;
            var pos = new Vector2(itemRect.Position.X, -posY + contentSize.Y * 0.5f);
            itemRect.Position = pos;
        }

        public override Vector2 GetContentSize(int count)
        {
            var boxSize = Content.Size;
            boxSize.Y = count * (PrefabRect.Size.Y + ItemsCellPadding.Y);
            return boxSize;
        }

    }

}
