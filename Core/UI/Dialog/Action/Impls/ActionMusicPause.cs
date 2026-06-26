using Cthangover.Core.Audio;
using Cthangover.Core.Scenes;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Pauses background music playback via AudioService without stopping the
    /// track. Can be resumed with ActionMusicPlay.
    /// </summary>
    public class ActionMusicPause : ActionCommand
    {
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoRun(DialogRuntime runtime)
        {
            var audioService = SceneContextNode.FindNode<AudioService>("AudioService");
            audioService?.PauseMusic(true);
        }
    }
}
