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
        public Playlist Playlist { get; set; }

        public MusicType LastMusicType { get; set; }

        public string LastMusicName { get; set; }
        
        public float LastMusicTime { get; set; }

        public string SavedAmbientMusicName { get; set; }

        public float SavedAmbientMusicTime { get; set; }
    }

}
