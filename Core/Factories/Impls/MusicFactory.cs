using System;

namespace Cthangover.Core.Factories.Impls
{
    /// <summary>
    /// Thin <c>AudioFactory</c> for background music tracks. The separate
    /// <c>"music"</c> group name matters for Godot's audio bus routing —
    /// music plays through a dedicated mixer channel with its own volume
    /// and reverb profile, distinct from the <c>"sounds"</c> bus used by
    /// <c>SoundFactory</c>.
    /// </summary>
    public class MusicFactory : AudioFactory
    {
        private static readonly Lazy<MusicFactory> instance = new(() => new MusicFactory());

        private MusicFactory() : base("music", 64) { }

        public static MusicFactory Instance => instance.Value;

        public override string GroupName => "music";
        
    }
}
