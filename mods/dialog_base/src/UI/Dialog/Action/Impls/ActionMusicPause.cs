using Cthangover.Core.Audio;
using Godot;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog.Action.Impls
{
    /// <summary>
    /// Pauses background music playback via IAudioService without stopping the
    /// track. Can be resumed with ActionMusicPlay.
    /// </summary>
    public class ActionMusicPause : ActionCommand
    {
        /// <summary>Pause is instant — the dialog continues without waiting for audio acknowledgment.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        /// <summary>Pauses the current music track without stopping it, so <see cref="ActionMusicPlay"/> can resume it.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
            var audioService = Scene.Find<Node>("AudioService") as IAudioService;
            audioService?.PauseMusic(true);
        }
    }
}
