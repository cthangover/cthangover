using System;
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
    /// </summary>
    public class AvatarFactory : Texture2DFactory
    {
        private static readonly Lazy<AvatarFactory> instance = new(() => new AvatarFactory());
        private AvatarFactory() : base("avatars", 64) { }

        public static AvatarFactory Instance => instance.Value;

        public override string GroupName => "avatars";

    }
}
