using Godot;

namespace Cthangover.Core.Items
{

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
