using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
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
    /// Deferred scene switch: calls SceneManager.SwitchScene via CallDeferred
    /// to avoid mid-queue scene tree mutations. NoWait ensures the dialog ends
    /// immediately — there's no return from a scene switch.
    /// <para/>
    /// Does NOT force the owning scenario into OneRun: re-runs are the scenario
    /// author's decision (is_one_run metadata) plus per-event conditions.
    /// Navigation menus (select + switch_scene) must survive into the next
    /// visit, and forcing OneRun recorded them in CompletedScenarioIds — after
    /// which FilterCompletedScenarios silently dropped the whole location menu
    /// on re-entry.
    /// </summary>
    public class ActionSwitchScene : ActionCommand
    {
        /// <summary>Target scene name passed to <see cref="SceneManager.SwitchScene"/>.</summary>
        public string SceneName { get; set; }
        /// <summary>Whether to transition with the dialog hidden. Default true.</summary>
        public bool HiddenMode { get; set; } = true;
        /// <summary>Transition speed parameter passed to the scene manager. Default 4.0.</summary>
        public float Speed { get; set; } = 4f;

        /// <summary>Scene switch is deferred — the dialog ends immediately with no return.</summary>
        public override WaitType WaitType { get; set; } = WaitType.NoWait;

        /// <summary>Defers <c>SceneManager.SwitchScene</c> so the tree is not mutated mid-queue; the owning scenario is not forced into OneRun.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
            if (string.IsNullOrEmpty(SceneName))
            {
                GameLogger.Log("DIALOG", "ActionSwitchScene: SceneName is null or empty", LogLevel.Error);
                return;
            }

            // No forced IsOneRun here: location navigation scenarios are meant to
            // re-run on every visit. Forcing it made DialogRuntime record the
            // scenario as completed, and the next visit to the same scene started
            // without its menu. One-time story scenes declare is_one_run: true
            // (or a condition:) in their metadata instead.

            GameLogger.Log("DIALOG", $"ActionSwitchScene: deferring switch to '{SceneName}'");

            var sceneManager = Scene.Find<SceneManager>("SceneManager");
            if (sceneManager != null)
            {
                Callable.From(() => sceneManager.SwitchScene(SceneName)).CallDeferred();
            }
            else
            {
                GameLogger.Log("DIALOG", $"ActionSwitchScene: SceneManager not found for '{SceneName}'", LogLevel.Error);
            }
        }
    }
}
