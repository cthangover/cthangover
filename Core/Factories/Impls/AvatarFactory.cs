using System;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Factories.Impls
{

    public class AvatarFactory : Texture2DFactory
    {
        private static readonly Lazy<AvatarFactory> instance = new(() => new AvatarFactory());
        private AvatarFactory() : base("avatars", 64) { }

        public static AvatarFactory Instance => instance.Value;

        public override string GroupName => "avatars";

    }
}
