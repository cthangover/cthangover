using System.Collections.Generic;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Messages;

namespace Cthangover.Core.Scenarios
{
    /// <summary>
    /// Handles the <c>notify</c> DSL command. Queues a notification text
    /// into <see cref="NotificationBuffer"/> for display in the top-left
    /// notification panel. This is a fire-and-forget command — it does not
    /// block dialog execution.
    /// </summary>
    public class NotifyCommandStrategy : IScenarioCommandStrategy
    {
        /// <summary>
        /// The lowercase DSL command word.
        /// </summary>
        public string Command => "notify";

        /// <summary>
        /// Extracts the notification text from the first positional argument
        /// and enqueues it into <see cref="NotificationBuffer"/>. Supports
        /// localization via the <c>key=</c> named parameter.
        /// </summary>
        /// <param name="dlg">The dialog queue (unused — notifications bypass dialog).</param>
        /// <param name="positional">Positional arguments; <c>positional[0]</c> is the notification text.</param>
        /// <param name="named">Named arguments; <c>"key"</c> is a locale lookup key.</param>
        /// <param name="arrowTarget">Unused by this command.</param>
        /// <param name="locale">Optional localization provider for <c>key=</c> resolution.</param>
        public void Execute(DialogQueue dlg, List<string> positional,
            Dictionary<string, string> named, string arrowTarget,
            ILocalizationProvider locale)
        {
            var text = positional.Count > 0 ? positional[0] : null;

            if (named.TryGetValue("key", out var key) && locale != null)
            {
                var loc = locale.Get(key);
                if (loc != null) text = loc;
            }

            if (text != null)
                NotificationBuffer.Add(text);
        }
    }
}
