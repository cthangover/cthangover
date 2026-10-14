using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Items
{
    /// <summary>
    /// Read-only contract for an inventory item resolved from JSON data
    /// (concrete implementation: <see cref="Item"/>). Exposes the display fields
    /// (name, description, cost, icon) plus the gameplay hooks: the
    /// <see cref="ItemType"/> flags that drive UI categorisation, and the
    /// <see cref="ItemAction"/> behaviour invoked on use. <see cref="Properties"/>
    /// is the mod-defined extension bag imported from extra JSON keys — core code
    /// never reads it, but custom item actions and scenario logic may.
    /// </summary>
    public interface IItem
    {
        /// <summary>Unique item ID in path style (e.g. <c>"food/ration"</c>); the factory lookup key.</summary>
        string ID          { get; }
        /// <summary>Display name (resolved locale string).</summary>
        string Name        { get; }
        /// <summary>Tooltip/info text (resolved locale string).</summary>
        string Description { get; }
        /// <summary>Base purchase price in the game economy.</summary>
        int Cost { get; }
        /// <summary>Loaded icon texture from the mod's <c>items</c> texture group.</summary>
        Texture2D Sprite { get; }
        /// <summary><see cref="ItemType"/> flags describing the item's categories.</summary>
        ItemType ItemType  { get; }
        /// <summary>On-use behaviour resolved via <see cref="IItemAction.ID"/>, or <c>null</c> when the item has no use effect.</summary>
        IItemAction ItemAction { get; }
        /// <summary>String-keyed extension bag imported from extra JSON keys (<c>[JsonExtensionData]</c>).</summary>
        PropertyData Properties { get; }
    }

}
