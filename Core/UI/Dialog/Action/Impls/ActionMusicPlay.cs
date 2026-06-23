using Cthangover.Core.Audio;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Dialog.Action;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    public class ActionMusicPlay : ActionCommand
    {
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        public override void DoRun(DialogRuntime runtime)
        {
            var audioService = SceneContextNode.FindNode<AudioService>("AudioService");
            audioService?.PauseMusic(false);
        }
    }
}
