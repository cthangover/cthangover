namespace Cthangover.Core.UI.Event
{
    /// <summary>
    /// Fires on each timer tick (~1 second intervals) from SceneEventController.
    /// Named IOnTimeEvent in code despite the file name being IOnTimerEvent.
    /// Used by systems that need periodic updates slower than per-frame.
    /// </summary>
    public interface IOnTimeEvent : IEventPriority
    {
        void OnTimerTick();
    }

}
