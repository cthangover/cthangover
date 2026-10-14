using System;
using Cthangover.Core.Factories;

namespace Cthangover.Mods.Audio
{
    /// <summary>
    /// Thin <c>AudioFactory</c> for background music tracks. The separate
    /// <c>"music"</c> group name matters for Godot's audio bus routing —
    /// music plays through a dedicated mixer channel with its own volume
    /// and reverb profile, distinct from the <c>"sounds"</c> bus used by
    /// <c>SoundFactory</c>.
    /// <para/>
    /// The single instance is owned by this assembly as a lazily created
    /// singleton, exposed through the static <see cref="Instance"/> facade.
    /// </summary>
    public class MusicFactory : AudioFactory
    {
        private static readonly Lazy<MusicFactory> instance = new(() => new MusicFactory());

        internal MusicFactory() : base("music", 64) { }

        /// <summary>The lazily created shared instance.</summary>
        public static MusicFactory Instance => instance.Value;

        /// <summary>Mod asset group scanned by this factory — files live under <c>mods/*/</c><b>music</b>.</summary>
        public override string GroupName => "music";

    }
}
