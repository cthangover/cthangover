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
    /// Starts background music playback via IAudioService. Uses MusicType.Ambient
    /// so the music layer is treated as atmospheric background rather than UI
    /// feedback. If the same music track is already playing, the audio service handles
    /// deduplication.
    /// </summary>
    public class ActionMusic : ActionCommand
    {
        /// <summary>Music track identifier resolved through the audio system. Empty strings are silently ignored.</summary>
        public string Music { get; set; }

        /// <summary>Music starts playing in the background — the dialog continues immediately.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        /// <summary>Starts the named track as ambient background music through <see cref="IAudioService"/>; empty names are ignored.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
            if (string.IsNullOrEmpty(Music))
                return;

            var audioService = Scene.Find<Node>("AudioService") as IAudioService;
            audioService?.PlayMusic(Music, MusicType.Ambient);
        }
    }
}
