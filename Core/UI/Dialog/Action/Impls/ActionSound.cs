using Cthangover.Core.Audio;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Dialog.Action;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
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
