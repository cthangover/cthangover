using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Event;

namespace Cthangover.Core.Relationship
{

    public class RecruitTickController : IOnTimeEvent
    {
        public int Priority => 0;

        private bool _registered;

        public void EnsureRegistered()
        {
            if (_registered)
                return;
            var eventController = SceneContextNode.FindNode<SceneEventController>("EventController");
            if (eventController == null)
                return;
            eventController.AddTimerTickEventListener(this);
            _registered = true;
        }

        public void OnTimerTick()
        {
            RecruitBehaviourRegistry.Instance.OnTick();
        }
    }

}
