namespace Cthangover.Core.UI.Event
{
    
    public interface IOnTimeEvent : IEventPriority
    {
        void OnTimerTick();
    }

}
