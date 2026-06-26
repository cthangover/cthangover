using Cthangover.Core.Audio;
using Cthangover.Core.Scenes;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Starts background music playback via AudioService. Uses MusicType.Ambient
    /// so the music layer is treated as atmospheric background rather than UI
    /// feedback. If the same music track is already playing, AudioService handles
    /// deduplication.
    /// </summary>
    public class ActionMusic : ActionCommand
    {
        public string Music { get; set; }

        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoRun(DialogRuntime runtime)
        {
            if (string.IsNullOrEmpty(Music))
                return;

            var audioService = SceneContextNode.FindNode<AudioService>("AudioService");
            audioService?.PlayMusic(Music, MusicType.Ambient);
        }
    }
}
