using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    public class ActionSwitchScene : ActionCommand
    {
        public string SceneName { get; set; }
        public bool HiddenMode { get; set; } = true;
        public float Speed { get; set; } = 4f;

        public override WaitType WaitType { get; set; } = WaitType.NoWait;

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
