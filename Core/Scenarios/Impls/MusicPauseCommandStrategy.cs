using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Handles the <c>music_pause</c> DSL command. Pauses the currently
    /// playing background music stream. Playback can be resumed later with
    /// <see cref="MusicPlayCommandStrategy"/>.
    /// </summary>
    public class MusicPauseCommandStrategy : IScenarioCommandStrategy
    {
        /// <inheritdoc/>
        public string Command => "music_pause";

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            dlg.MusicPause();
        }
    }
}
