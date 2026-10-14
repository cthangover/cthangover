using Cthangover.Core.Bootstrap;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Thin <c>Texture2DFactory</c> for full-size UI panel textures
    /// (frames, overlays, menu backgrounds). Uses the top-level
    /// <c>"ui"</c> group so that UI textures sit alongside their companion
    /// files (theme JSON, font atlases) without needing a separate
    /// subdirectory per asset type.
    /// <para/>
    /// The single instance is owned by the <see cref="AppServices"/>
    /// composition root; the static facade stays as the public API during
    /// migration, with laziness preserved by the owning <c>Lazy</c> slot.
    /// </summary>
    public class UITextureFactory : Texture2DFactory
    {
        internal UITextureFactory() : base("ui", 64) { }

        /// <summary>Facade over the AppServices-owned instance; kept as the public API during migration.</summary>
        public static UITextureFactory Instance => AppServices.Current.UITextures.Value;

        /// <summary>Mod asset group scanned by this factory — files live under <c>mods/*/</c><b>ui</b>.</summary>
        public override string GroupName => "ui";
    }
}
