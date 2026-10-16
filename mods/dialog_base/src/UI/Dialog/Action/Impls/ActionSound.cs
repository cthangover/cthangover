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
    /// Plays a sound through the audio service using the UI sound channel (SoundType.UI).
    /// NoWait so the sound plays and the dialog continues immediately — no
    /// synchronization with audio completion.
    /// </summary>
    public class ActionSound : ActionCommand
    {
        /// <summary>Sound effect identifier resolved through <see cref="IAudioService"/>. Played on the UI channel.</summary>
        public string Sound { get; set; }

        /// <summary>Sound is fire-and-forget — the dialog continues immediately without synchronization.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        /// <summary>Plays <see cref="Sound"/> on the UI sound channel through <see cref="IAudioService"/>; empty names are ignored.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
            if (string.IsNullOrEmpty(Sound))
                return;

            var audioService = Scene.Find<Node>("AudioService") as IAudioService;
            audioService?.PlaySound(Sound, SoundType.UI);
        }
    }
}
