using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Event;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
    /// <summary>
    /// Timeline-based update action: registers itself as an IOnUpdateEvent
    /// subscriber and fires OnUpdateCallback(float t) each frame with a normalized
    /// progress value (0→1) over WaitTime seconds. WaitType is WaitEvent so the
    /// dialog runtime pauses until elapsed >= WaitTime, at which point isRunning
    /// flips to false and the runtime advances. OnStartCallback/OnFinishCallback
    /// bracket the animation for setup/teardown. Fixed 60fps step for deterministic
    /// progress regardless of actual frame rate.
    /// </summary>
    public class ActionTimedUpdate : ActionCommand, IOnUpdateEvent
    {
        public int Priority => 0;

        public System.Action OnStartCallback { get; set; }
        public System.Action<float> OnUpdateCallback { get; set; }
        public System.Action OnFinishCallback { get; set; }

        public override WaitType WaitType { get; set; } = WaitType.WaitEvent;
        public override float WaitTime { get; set; } = 1f;

        private float elapsed;
        private bool isRunning;

        public override void DoConstruct()
        {
            base.DoConstruct();
            var controller = SceneContextNode.FindNode<SceneEventController>("EventController");
            controller?.AddUpdateEventListener(this);
        }

        public override void DoRun(DialogRuntime runtime)
        {
            elapsed = 0f;
            isRunning = true;
            OnStartCallback?.Invoke();
        }

        public override void DoDestruct()
        {
            base.DoDestruct();
            isRunning = false;
            var controller = SceneContextNode.FindNode<SceneEventController>("EventController");
            controller?.RemoveUpdateEventListener(this);
            OnFinishCallback?.Invoke();
        }

        void IOnUpdateEvent.OnUpdate()
        {
            if (!isRunning)
                return;

            elapsed += 1f / 60f;

            var t = WaitTime > 0f ? Mathf.Min(elapsed / WaitTime, 1f) : 1f;
            OnUpdateCallback?.Invoke(t);

            if (elapsed >= WaitTime)
            {
                isRunning = false;
            }
        }
    }
}
