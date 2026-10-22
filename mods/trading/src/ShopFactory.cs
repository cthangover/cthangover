using System;
using Cthangover.Core.Factories;

namespace Cthangover.Mods.Trading
{
    /// <summary>Shops collected by the shared entity resolver (<c>"Kind": "shop"</c>).</summary>
    public class ShopFactory : FileFactory<ShopInfo>
    {
        private static readonly Lazy<ShopFactory> instance = new(() => new ShopFactory());

        /// <summary>Shared instance.</summary>
        public static ShopFactory Instance => instance.Value;
    }
}
