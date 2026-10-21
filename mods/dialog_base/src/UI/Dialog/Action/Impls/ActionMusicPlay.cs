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
    /// Resumes paused background music. Paired with ActionMusicPause.
    /// </summary>
    public class ActionMusicPlay : ActionCommand
    {
        /// <summary>Resume is instant — the dialog continues without waiting for audio.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        /// <summary>Resumes music paused by <see cref="ActionMusicPause"/>.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
            var audioService = Scene.Find<Node>("AudioService") as IAudioService;
            audioService?.PauseMusic(false);
        }
    }
}
