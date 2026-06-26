using System;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Thin <c>Texture2DFactory</c> for inventory item icons. Separated
    /// from the general UI texture factory so that the item sprite cache
    /// uses a dedicated LRU budget — the inventory UI may render dozens of
    /// sprites simultaneously and should evict its own stale entries rather
    /// than competing with dialog backgrounds or avatars.
    /// </summary>
    public class ItemSpriteFactory : Texture2DFactory
    {
        private static readonly Lazy<ItemSpriteFactory> instance = new(() => new ItemSpriteFactory());
        private ItemSpriteFactory() : base("items", 64) { }

        public static ItemSpriteFactory Instance => instance.Value;

        public override string GroupName => "items";
        
    }
}
