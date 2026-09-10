using System;
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
    /// </summary>
    public class BackgroundFactory : Texture2DFactory
    {
        private static readonly Lazy<BackgroundFactory> instance = new(() => new BackgroundFactory());

        private BackgroundFactory() : base("backgrounds", 64) { }

        public static BackgroundFactory Instance => instance.Value;

        public override string GroupName => "backgrounds";

    }
}
