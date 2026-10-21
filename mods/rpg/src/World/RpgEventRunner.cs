using System.Text;
using Cthangover.Core.Utils;
using Cthangover.Mods.Dialog.Scenarios;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Fires a map event. A teleport moves the player natively (no dialog, so a
    /// door costs nothing). Anything else becomes one dialog in the shell's
    /// dialog box: the event's scenario body followed by its inline commands —
    /// the same composition the interactive objects use for click dialogs, so
    /// every DSL command, action and condition is available to map events.
    /// </summary>
    public static class RpgEventRunner
    {
        /// <summary>Runs the event; returns false when it has nothing to run or the dialog box is busy.</summary>
        public static bool Run(RpgMapEvent mapEvent, string currentMap)
        {
            if (mapEvent == null)
                return false;

            if (mapEvent.Teleport != null)
            {
                var target = mapEvent.Teleport;
                RpgWorldHost.Teleport(new RpgTeleport
                {
                    Map = string.IsNullOrEmpty(target.Map) ? currentMap : target.Map,
                    X = target.X,
                    Y = target.Y,
                    Dir = target.Dir,
                });
                return true;
            }

            var text = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(mapEvent.Scenario))
            {
                var body = RpgScenarioSource.ReadBody(mapEvent.Scenario);
                if (body == null)
                    GameLogger.Log("RPG", $"event '{mapEvent.Id}' on '{currentMap}': scenario '{mapEvent.Scenario}' not found", LogLevel.Error);
                else
                    text.AppendLine(body);
            }
            foreach (var line in mapEvent.Commands ?? new())
                text.AppendLine(line);
            if (text.Length == 0)
                return false;

            var dialogBox = RpgInput.FindDialogBox();
            if (dialogBox == null || dialogBox.IsRunning)
                return false;

            var dialog = ScenarioParser.Parse(text.ToString());
            if (dialog?.Queue == null || dialog.Queue.Count == 0)
                return false;

            GameLogger.Log("RPG", $"event '{mapEvent.Id}' on '{currentMap}'");
            return dialogBox.SetDialogQueueAndRun(dialog, null, 0, null);
        }
    }
}
