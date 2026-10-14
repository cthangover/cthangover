using System;
using Cthangover.Core.Factories;

namespace Cthangover.Mods.Audio
{
    /// <summary>
    /// Thin <c>AudioFactory</c> for sound effects. While structurally
    /// identical to <c>MusicFactory</c>, the distinct group name
    /// <c>"sounds"</c> routes audio through a separate Godot mixer bus —
    /// sound effects bypass the music channel's reverb and ducking
    /// processing, and have their own volume slider in settings.
    /// <para/>
    /// The single instance is owned by this assembly as a lazily created
    /// singleton, exposed through the static <see cref="Instance"/> facade.
    /// </summary>
    public class SoundFactory : AudioFactory
    {
        private static readonly Lazy<SoundFactory> instance = new(() => new SoundFactory());

        internal SoundFactory() : base("sounds", 64) { }

        /// <summary>The lazily created shared instance.</summary>
        public static SoundFactory Instance => instance.Value;

        /// <summary>Mod asset group scanned by this factory — files live under <c>mods/*/</c><b>sounds</b>.</summary>
        public override string GroupName => "sounds";

    }
}
