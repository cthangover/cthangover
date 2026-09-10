using Cthangover.Core.Audio;
using Cthangover.Core.Scenes;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Resumes paused background music. Paired with ActionMusicPause.
    /// </summary>
    public class ActionMusicPlay : ActionCommand
    {
        /// <summary>Resume is instant — the dialog continues without waiting for audio.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoRun(DialogRuntime runtime)
        {
            var audioService = SceneContextNode.FindNode<AudioService>("AudioService");
            audioService?.PauseMusic(false);
        }
    }
}
