using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Handles the <c>music_play</c> DSL command. Resumes the currently
    /// paused background music stream. Has no effect if music is already playing.
    /// </summary>
    public class MusicPlayCommandStrategy : IScenarioCommandStrategy
    {
        /// <inheritdoc/>
        public string Command => "music_play";

        /// <inheritdoc/>
        public void Execute(DialogQueue dlg, List<string> positional, Dictionary<string, string> named, string arrowTarget, ILocalizationProvider locale)
        {
            dlg.MusicPlay();
        }
    }
}
