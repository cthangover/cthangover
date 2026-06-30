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
        /// <summary>Update priority. Value 0 — runs before most other listeners.</summary>
        public int Priority => 0;

        /// <summary>Fires once when <see cref="DoRun"/> is called, before the first update frame.</summary>
        public System.Action OnStartCallback { get; set; }
        /// <summary>Fires each frame during the active period with a normalized progress value t (0→1) over <see cref="WaitTime"/> seconds.</summary>
        public System.Action<float> OnUpdateCallback { get; set; }
        /// <summary>Fires once when the elapsed time exceeds <see cref="WaitTime"/> or when destructed.</summary>
        public System.Action OnFinishCallback { get; set; }

        /// <summary>Pauses the dialog until the elapsed time meets WaitTime — the runtime polls via update events.</summary>
        public override WaitType WaitType { get; set; } = WaitType.WaitEvent;
        /// <summary>Total duration in seconds over which <see cref="OnUpdateCallback"/> receives normalized progress.</summary>
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
