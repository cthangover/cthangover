using Cthangover.Core.UI.Event;
using Godot;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Interactive;

namespace Cthangover.Mods.Dialog.Action.Impls
{
    /// <summary>
    /// Timeline-based update action: registers itself as an IOnUpdateEvent
    /// subscriber and fires OnUpdateCallback(float t) each frame with a normalized
    /// progress value (0→1) over WaitTime seconds. WaitType is WaitEvent so the
    /// dialog runtime pauses until elapsed >= WaitTime, at which point the action
    /// completes itself through <see cref="DialogBox.CompleteAction"/> (event-driven —
    /// no DialogBox poll) and the box advances the queue, scheduling the timer of a
    /// following <c>delay</c> like any other advance. OnStartCallback/OnFinishCallback
    /// bracket the animation for setup/teardown. Progress follows the controller's
    /// process delta, so the timeline lasts WaitTime seconds of game time at any frame
    /// rate (and scales with <c>Engine.TimeScale</c> exactly like <c>delay</c> timers).
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

        /// <summary>Pauses the dialog until the elapsed time meets WaitTime — the action then completes itself.</summary>
        public override WaitType WaitType { get; set; } = WaitType.WaitEvent;
        /// <summary>Total duration in seconds over which <see cref="OnUpdateCallback"/> receives normalized progress.</summary>
        public override float WaitTime { get; set; } = 1f;

        private float elapsed;
        private bool isRunning;
        private DialogRuntime runtime;
        private SceneEventController controller;

        /// <summary>Registers this action as an <see cref="IOnUpdateEvent"/> listener on the scene's event controller.</summary>
        public override void DoConstruct()
        {
            base.DoConstruct();
            controller = Scene.Find<SceneEventController>("EventController");
            controller?.AddUpdateEventListener(this);
        }

        /// <summary>Starts the timeline: resets elapsed time and fires <see cref="OnStartCallback"/> before the first frame.</summary>
        public override void DoRun(DialogRuntime runtime)
        {
            this.runtime = runtime;
            elapsed = 0f;
            isRunning = true;
            OnStartCallback?.Invoke();
        }

        /// <summary>Detaches from the update controller, stops ticking and fires <see cref="OnFinishCallback"/>.</summary>
        public override void DoDestruct()
        {
            base.DoDestruct();
            isRunning = false;
            runtime = null;
            controller?.RemoveUpdateEventListener(this);
            controller = null;
            OnFinishCallback?.Invoke();
        }

        void IOnUpdateEvent.OnUpdate()
        {
            if (!isRunning)
                return;

            elapsed += (float)(controller?.GetProcessDeltaTime() ?? 0d);

            var t = WaitTime > 0f ? Mathf.Min(elapsed / WaitTime, 1f) : 1f;
            OnUpdateCallback?.Invoke(t);

            if (elapsed < WaitTime)
                return;

            // Advancing destructs this action, which clears the runtime field:
            // keep the runtime in a local for the whole completion.
            isRunning = false;
            var owner = runtime;
            if (owner?.DialogBox != null)
            {
                owner.DialogBox.CompleteAction(this);
            }
            else if (owner != null)
            {
                owner.Next();
                owner.Run();
            }
        }
    }
}
