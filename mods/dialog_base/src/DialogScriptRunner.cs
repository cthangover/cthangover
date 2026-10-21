using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Interactive;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI;
using Cthangover.Core.Utils;
using Cthangover.Mods.Dialog.Scenarios;

using Cthangover.Mods.Interface.Base;
namespace Cthangover.Mods.Dialog
{
    /// <summary>
    /// Dialog execution entry point of the dialog_base engine mod, parked at
    /// /root/DialogRunner. Implements the core <c>IInteractiveDialogRunner</c>
    /// contract: interactive click-dialogs (a scenario file combined with
    /// inline DSL commands) and standalone inline DSL commands are parsed here
    /// and executed through the mod-owned dialog runtime and the shared
    /// <see cref="DialogBox"/>.
    /// </summary>
    public partial class DialogScriptRunner : ModNode, IInteractiveDialogRunner
    {
        /// <summary>Runs the click action of the interactive definition with the given id through the shared DialogBox. The definition is resolved through the interactive_base factory. Returns false when the definition is unknown, the box is missing, busy, or the definition carries no runnable commands.</summary>
        public bool TryExecuteClickDialog(string definitionId)
        {
            var definition = InteractiveFactory.Instance.Get(definitionId);
            if (definition == null)
            {
                GameLogger.Log("INTERACTIVE", $"TryExecuteClickDialog: definition '{definitionId}' not found", LogLevel.Error);
                return false;
            }

            var dialogBox = SceneContextNode.FindNode<DialogBox>("DialogBox");
            if (dialogBox == null)
            {
                GameLogger.Log("INTERACTIVE", "TryExecuteClickDialog: DialogBox not found", LogLevel.Error);
                return false;
            }

            if (dialogBox.IsRunning)
            {
                GameLogger.Log("INTERACTIVE", "TryExecuteClickDialog: dialog already running, skipping");
                return false;
            }

            var dlg = BuildClickDialog(definition);
            if (dlg == null || dlg.Queue.Count == 0)
                return false;

            return dialogBox.SetDialogQueueAndRun(dlg, null, 0, null);
        }

        /// <summary>Runs inline DSL commands (hover effects and similar) through a standalone dialog runtime.</summary>
        public void ExecuteInlineCommands(string dsl)
        {
            if (string.IsNullOrWhiteSpace(dsl))
                return;

            try
            {
                var dlg = ScenarioParser.Parse(dsl);
                if (dlg?.Queue == null || dlg.Queue.Count == 0)
                    return;

                var runtime = new DialogRuntime();
                runtime.SetDialogQueueAndRun(null, dlg.Queue, null, 0);
                runtime.Run();
            }
            catch (System.Exception ex)
            {
                GameLogger.Log("INTERACTIVE", $"ExecuteInlineCommands failed: {ex.Message}", LogLevel.Error);
            }
        }

        private DialogQueue BuildClickDialog(InteractiveDefinition def)
        {
            if (def?.Actions?.OnClick == null)
                return null;

            var clickAction = def.Actions.OnClick;
            var combinedText = "";

            if (!string.IsNullOrEmpty(clickAction.Scenario))
            {
                var modId = def.ModId;
                if (string.IsNullOrEmpty(modId))
                    modId = ResolveModIdForScenario(clickAction.Scenario);

                if (!string.IsNullOrEmpty(modId))
                {
                    var scenarioText = ModManager.Instance.ReadFileText(modId, clickAction.Scenario);
                    if (!string.IsNullOrEmpty(scenarioText))
                    {
                        var (metadata, body) = ScenarioMetadata.ParseMetadata(scenarioText);
                        combinedText += body + "\n";
                    }
                }
            }

            if (clickAction.Commands != null && clickAction.Commands.Length > 0)
            {
                combinedText += string.Join("\n", clickAction.Commands);
            }

            if (string.IsNullOrWhiteSpace(combinedText))
                return null;

            return ScenarioParser.Parse(combinedText);
        }

        private string ResolveModIdForScenario(string scenarioPath)
        {
            var defs = ScenarioDefs.CollectDefinitions();
            var fileName = System.IO.Path.GetFileNameWithoutExtension(
                scenarioPath.Replace('\\', '/').Split('/')[^1]);

            foreach (var kv in defs)
            {
                foreach (var sd in kv.Value)
                {
                    if (sd.Name == fileName)
                        return sd.ModId;
                }
            }

            return null;
        }
    }
}