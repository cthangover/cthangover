using Cthangover.Core.Audio;
using Cthangover.Core.Scenes;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Plays a sound through the AudioService using the UI sound channel (SoundType.UI).
    /// NoWait so the sound plays and the dialog continues immediately — no
    /// synchronization with audio completion.
    /// </summary>
    public class ActionSound : ActionCommand
    {
        public string Sound { get; set; }

        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoRun(DialogRuntime runtime)
        {
            if (string.IsNullOrEmpty(Sound))
                return;

            var audioService = SceneContextNode.FindNode<AudioService>("AudioService");
            audioService?.PlaySound(Sound, SoundType.UI);
        }
    }
}
