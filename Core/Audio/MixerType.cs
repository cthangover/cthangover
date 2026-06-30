namespace Cthangover.Core.Audio
{
    /// <summary>
    /// Identifies one of the three hardware audio buses. Each bus has
    /// independent volume and mute state, allowing e.g. music to be muted
    /// while SFX and ambient continue playing.
    /// </summary>
    public enum MixerType
    {
        /// <summary>Bus for short sound effects (SFX).</summary>
        Sounds,
        /// <summary>Bus for background music.</summary>
        Musics,
        /// <summary>Bus for ambient loops (dynamically crossfaded).</summary>
        Ambient
    }
}
