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
    /// to avoid mid-queue scene tree mutations. Marks the owning ExecutableEvent
    /// as OneRun so the same trigger doesn't re-fire after the scene reloads.
    /// NoWait ensures the dialog ends immediately — there's no return from a
    /// scene switch.
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

        /// <summary>Marks the dialog locker as OneRun and defers <c>SceneManager.SwitchScene</c> so the tree is not mutated mid-queue.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
            if (string.IsNullOrEmpty(SceneName))
            {
                GameLogger.Log("DIALOG", "ActionSwitchScene: SceneName is null or empty", LogLevel.Error);
                return;
            }

            var locker = runtime.DialogBox?.Locker;
            if (locker != null)
                locker.IsOneRun = true;

            GameLogger.Log("DIALOG", $"ActionSwitchScene: deferring switch to '{SceneName}'");

            var sceneManager = SceneContextNode.FindNode<SceneManager>("SceneManager");
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
