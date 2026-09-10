namespace Cthangover.Core.Events
{
    [LifecycleEvent]
    [EventKey("battle_ended")]
    public class BattleEndedEvent : ModEvent
    {
        public string Result { get; init; }

        public int Turns { get; init; }
    }
}
