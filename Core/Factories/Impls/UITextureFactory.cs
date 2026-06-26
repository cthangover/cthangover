using System;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Thin <c>Texture2DFactory</c> for full-size UI panel textures
    /// (frames, overlays, menu backgrounds). Uses the top-level
    /// <c>"ui"</c> group so that UI textures sit alongside their companion
    /// files (theme JSON, font atlases) without needing a separate
    /// subdirectory per asset type.
    /// </summary>
    public class UITextureFactory : Texture2DFactory
    {
        private static readonly Lazy<UITextureFactory> instance = new(() => new UITextureFactory());
        private UITextureFactory() : base("ui", 64) { }

        public static UITextureFactory Instance => instance.Value;

        public override string GroupName => "ui";
    }
}
