using Godot;

namespace Cthangover.Core.Items
{
    /// <summary>
    /// Immutable item definition contract. All properties are read-only
    /// (only getters) because items are <b>prototype</b> objects created
    /// once by <c>ItemFactory</c> and shared across every inventory slot
    /// that holds them — mutating an item would corrupt every stack.
    ///
    /// <c>ItemType</c> is a <c>[Flags]</c> enum so a single item can
    /// belong to multiple categories (e.g. a quest food item is both
    /// <c>Quest</c> and <c>Food</c>). <c>ItemAction</c> is attached to
    /// the definition rather than the inventory slot because an item's
    /// on-use behaviour is intrinsic to what the item <i>is</i>, not to
    /// where it happens to be stored.
    /// </summary>
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
