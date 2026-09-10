namespace Cthangover.Core.Events
{
    [LifecycleEvent]
    [EventKey("battle_started")]
    public class BattleStartedEvent : ModEvent
    {
        public string BattleType { get; init; }
        public string[] Enemies { get; init; }
    }
}
