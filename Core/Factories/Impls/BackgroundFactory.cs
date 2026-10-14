using Cthangover.Core.Bootstrap;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Thin <c>Texture2DFactory</c> for scene backgrounds. Isolated from
    /// the general UI texture factory because backgrounds are typically
    /// full-resolution images that consume significantly more GPU memory
    /// — a separate cache budget prevents background evictions from
    /// cascading into UI sprite thrashing.
    /// <para/>
    /// The single instance is owned by the <see cref="AppServices"/>
    /// composition root; the static facade stays as the public API during
    /// migration, with laziness preserved by the owning <c>Lazy</c> slot.
    /// </summary>
    public class BackgroundFactory : Texture2DFactory
    {
        internal BackgroundFactory() : base("backgrounds", 64) { }

        /// <summary>Facade over the AppServices-owned instance; kept as the public API during migration.</summary>
        public static BackgroundFactory Instance => AppServices.Current.Backgrounds.Value;

        /// <summary>Mod asset group scanned by this factory — files live under <c>mods/*/</c><b>backgrounds</b>.</summary>
        public override string GroupName => "backgrounds";

    }
}
