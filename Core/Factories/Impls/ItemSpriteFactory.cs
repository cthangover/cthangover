using System;

namespace Cthangover.Core.Factories.Impls
{
    public class ItemSpriteFactory : Texture2DFactory
    {
        private static readonly Lazy<ItemSpriteFactory> instance = new(() => new ItemSpriteFactory());
        private ItemSpriteFactory() : base("items", 64) { }

        public static ItemSpriteFactory Instance => instance.Value;

        public override string GroupName => "items";
        
    }
}
