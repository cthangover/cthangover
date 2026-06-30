using Cthangover.Core.UI.Event;
using Godot;

namespace Cthangover.Core.Settings
{

    public class TimeTickController : IOnTimeEvent
    {
        public int Priority => 0;

        private bool _registered;

        public void EnsureRegistered(Node node)
        {
            if (_registered)
                return;
            var eventController = node.GetTree()?.Root?.FindChild("EventController", true, false) as SceneEventController;
            if (eventController == null)
                return;
            eventController.AddTimerTickEventListener(this);
            _registered = true;
        }

        public void OnTimerTick()
        {
            GameData.Instance.Runtime.Time.AddTick();
        }
    }

}
