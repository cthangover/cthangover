using Cthangover.Core.Factories;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Thin <c>Texture2DFactory</c> for character portrait avatars. The
    /// separate factory (as opposed to reusing <c>UITextureFactory</c>)
    /// isolates avatar images into their own mod group with a dedicated
    /// cache budget, so frequently-swapped dialog portraits don't evict
    /// other UI textures under memory pressure.
    /// <para/>
    /// The factory lives in the characters engine mod; its singleton is also
    /// published to the kernel through <see cref="AvatarProviderRegistry"/> by
    /// <c>CharactersModInitializer</c>, so core-side consumers resolve
    /// avatars without referencing this assembly.
    /// </summary>
    public class AvatarFactory : Texture2DFactory, IAvatarProvider
    {
        internal AvatarFactory() : base("avatars", 64) { }

        /// <summary>Process-wide singleton for this mod assembly; the public API for avatar lookups.</summary>
        public static AvatarFactory Instance { get; } = new AvatarFactory();

        /// <summary>Mod asset group scanned by this factory — files live under <c>mods/*/</c><b>avatars</b>.</summary>
        public override string GroupName => "avatars";

    }
}
