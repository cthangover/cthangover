using System;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Factories.Impls
{
    public class BackgroundFactory : Texture2DFactory
    {
        private static readonly Lazy<BackgroundFactory> instance = new(() => new BackgroundFactory());

        private BackgroundFactory() : base("backgrounds", 64) { }

        public static BackgroundFactory Instance => instance.Value;

        public override string GroupName => "backgrounds";

    }
}
