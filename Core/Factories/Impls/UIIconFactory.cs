using System;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Thin <c>Texture2DFactory</c> for small UI icons (buttons, indicators,
    /// status badges). Isolated from <c>UITextureFactory</c> under the
    /// <c>"ui/icons"</c> group so that icon atlases can be organised
    /// separately from full UI panel textures, and each gets its own cache
    /// budget proportional to its expected memory footprint.
    /// </summary>
    public class UIIconFactory : Texture2DFactory
    {
        private static readonly Lazy<UIIconFactory> instance = new(() => new UIIconFactory());
        private UIIconFactory() : base("ui/icons", 64) { }

        public static UIIconFactory Instance => instance.Value;

        public override string GroupName => "ui/icons";

    }
}
