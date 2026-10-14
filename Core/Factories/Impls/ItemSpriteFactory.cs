using Cthangover.Core.Bootstrap;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Thin <c>Texture2DFactory</c> for inventory item icons. Separated
    /// from the general UI texture factory so that the item sprite cache
    /// uses a dedicated LRU budget — the inventory UI may render dozens of
    /// sprites simultaneously and should evict its own stale entries rather
    /// than competing with dialog backgrounds or avatars.
    /// <para/>
    /// The single instance is owned by the <see cref="AppServices"/>
    /// composition root; the static facade stays as the public API during
    /// migration, with laziness preserved by the owning <c>Lazy</c> slot.
    /// </summary>
    public class ItemSpriteFactory : Texture2DFactory
    {
        internal ItemSpriteFactory() : base("items", 64) { }

        /// <summary>Facade over the AppServices-owned instance; kept as the public API during migration.</summary>
        public static ItemSpriteFactory Instance => AppServices.Current.ItemSprites.Value;

        /// <summary>Mod asset group scanned by this factory — files live under <c>mods/*/</c><b>items</b>.</summary>
        public override string GroupName => "items";
        
    }
}
