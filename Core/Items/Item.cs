using Godot;

namespace Cthangover.Core.Items
{
    /// <summary>
    /// Concrete item definition. All properties are settable — despite
    /// <c>IItem</c> exposing only getters — because <c>ItemFactory</c>
    /// constructs instances via property initialisers. The
    /// <c>Ration</c> constant exposes the most commonly referenced item
    /// ID as a compile-time literal so that hunger-recovery logic and
    /// quest scripts can reference it without a magic string.
    /// </summary>
    public class Item : IItem
    {
        public const string Ration = "food/ration";
        
        public string      ID          { get; set; }
        public string      Name        { get; set; }
        public string      Description { get; set; }
        public int         Cost        { get; set; }
        public Texture2D   Sprite      { get; set; }
        public ItemType    ItemType    { get; set; }
        public IItemAction ItemAction  { get; set; }
    }

}
