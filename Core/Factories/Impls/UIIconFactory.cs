using Cthangover.Core.Bootstrap;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Thin <c>Texture2DFactory</c> for small UI icons (buttons, indicators,
    /// status badges). Isolated from <c>UITextureFactory</c> under the
    /// <c>"ui/icons"</c> group so that icon atlases can be organised
    /// separately from full UI panel textures, and each gets its own cache
    /// budget proportional to its expected memory footprint.
    /// <para/>
    /// The single instance is owned by the <see cref="AppServices"/>
    /// composition root; the static facade stays as the public API during
    /// migration, with laziness preserved by the owning <c>Lazy</c> slot.
    /// </summary>
    public class UIIconFactory : Texture2DFactory
    {
        internal UIIconFactory() : base("ui/icons", 64) { }

        /// <summary>Facade over the AppServices-owned instance; kept as the public API during migration.</summary>
        public static UIIconFactory Instance => AppServices.Current.UIIcons.Value;

        /// <summary>Mod asset group scanned by this factory — files live under <c>mods/*/</c><b>ui/icons</b>.</summary>
        public override string GroupName => "ui/icons";

    }
}
