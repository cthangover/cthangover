namespace Cthangover.Core.Audio
{

    /// <summary>
    /// Mutable playback state carried across track switches and scene
    /// transitions. Holds the active Playlist, the last played track/type/time,
    /// and the saved ambient state (<c>SavedAmbientMusicName</c> /
    /// <c>SavedAmbientMusicTime</c>) used to restore ambient music after
    /// combat interruptions.
    /// </summary>
    public class PlaylistContext
    {
        /// <summary>
        /// The currently active playlist, resolved by scene name.
        /// </summary>
        public Playlist Playlist { get; set; }

        /// <summary>
        /// The <see cref="MusicType"/> of the last played (or
        /// currently playing) track. Initialised to <c>Force</c>
        /// and normalised to <c>Ambient</c> on the first playlist
        /// init.
        /// </summary>
        public MusicType LastMusicType { get; set; }

        /// <summary>
        /// Asset name of the last track. Saved across stop/start
        /// cycles so <c>EnabledAutoPlay</c> can restore it from
        /// the factory.
        /// </summary>
        public string LastMusicName { get; set; }
        
        /// <summary>
        /// Playback position of the last track, in seconds. Captured
        /// when auto-play is disabled so the track can resume near
        /// where it left off.
        /// </summary>
        public float LastMusicTime { get; set; }

        /// <summary>
        /// Ambient track name saved when entering combat. Restored
        /// on return to ambient so the player hears the same
        /// background music that was interrupted.
        /// </summary>
        public string SavedAmbientMusicName { get; set; }

        /// <summary>
        /// Ambient track position (seconds) saved alongside
        /// <see cref="SavedAmbientMusicName"/>. Used to create a
        /// trimmed OGG stream via <c>OggPacketParser</c> when
        /// the saved time exceeds 0.5s.
        /// </summary>
        public float SavedAmbientMusicTime { get; set; }
    }

}
