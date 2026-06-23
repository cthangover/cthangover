using Godot;

namespace Cthangover.Core.Items
{

    public interface IItem
    {
        string ID          { get; }
        string Name        { get; }
        string Description { get; }
        int Cost { get; }
        Texture2D Sprite { get; }
        ItemType ItemType  { get; }
        IItemAction ItemAction { get; }
    }

}
