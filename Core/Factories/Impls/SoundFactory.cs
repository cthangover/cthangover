using System;
using Cthangover.Core.Factories;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Thin <c>AudioFactory</c> for sound effects. While structurally
    /// identical to <c>MusicFactory</c>, the distinct group name
    /// <c>"sounds"</c> routes audio through a separate Godot mixer bus —
    /// sound effects bypass the music channel's reverb and ducking
    /// processing, and have their own volume slider in settings.
    /// </summary>
    public class SoundFactory : AudioFactory
    {
        private static readonly Lazy<SoundFactory> instance = new(() => new SoundFactory());

        private SoundFactory() : base("sounds", 64) { }

        public static SoundFactory Instance => instance.Value;

        public override string GroupName => "sounds";

    }
}
