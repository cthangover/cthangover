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
        /// <summary>
        /// Transient signal used to force a specific track. Normalised to
        /// <c>Ambient</c> during playlist initialisation so it never
        /// persists across scene transitions.
        /// </summary>
        Force,
        /// <summary>
        /// Battle music. Transitioning to Combat saves the current
        /// Ambient track and position for later restoration.
        /// </summary>
        Combat,
        /// <summary>
        /// Background exploration music. The default type; restored
        /// with the saved track when returning from Combat.
        /// </summary>
        Ambient
    }
    
}
