namespace Cthangover.Core.Audio
{
    
    /// <summary>
    /// Tags music tracks by gameplay context. <c>Force</c> is a transient
    /// signal — Normalized to <c>Ambient</c> during playlist init.
    /// The <c>Combat</c> ↔ <c>Ambient</c> transition is stateful:
    /// switching to Combat saves the ambient track and playback position
    /// so it can resume seamlessly when combat ends.
    /// </summary>
    public enum MusicType
    {
        Force,
        Combat,
        Ambient
    }
    
}
