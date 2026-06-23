using System;
using Cthangover.Core.Factories;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Factories.Impls
{
    public class SoundFactory : AudioFactory
    {
        private static readonly Lazy<SoundFactory> instance = new(() => new SoundFactory());

        private SoundFactory() : base("sounds", 64) { }

        public static SoundFactory Instance => instance.Value;

        public override string GroupName => "sounds";

    }
}
