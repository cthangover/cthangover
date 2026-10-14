namespace Cthangover.Core.Events
{
    /// <summary>
    /// Published by the recruiting mod when a captured recruit is placed into
    /// the active party (see <c>RecruitPartyBridge.AddToSquad</c>). Purely
    /// informational — the core party data is already updated when subscribers
    /// see the event.
    /// </summary>
    [EventKey("recruit_joined")]
    public class RecruitJoinedEvent : ModEvent
    {
        /// <summary>Instance id of the recruit.</summary>
        public string RecruitId { get; init; }
        /// <summary>Character template id the recruit was spawned from.</summary>
        public string RecruitName { get; init; }
    }
}
