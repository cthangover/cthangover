using System;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Dialog.Action;
using Cthangover.Core.UI.Event;
using Godot;

namespace Cthangover.Core.UI.Dialog.Action.Impls
{
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
