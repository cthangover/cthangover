namespace Cthangover.Core.Audio
{

    public class PlaylistContext
    {
        public Playlist Playlist { get; set; }

        public MusicType LastMusicType { get; set; }

        public string LastMusicName { get; set; }
        
        public float LastMusicTime { get; set; }
    }

}
