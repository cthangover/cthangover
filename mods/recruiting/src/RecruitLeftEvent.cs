namespace Cthangover.Core.Events
{
    /// <summary>
    /// Published by the recruiting mod when a recruit leaves the active party
    /// (see <c>RecruitPartyBridge.RemoveFromSquad</c>). The placeholder card and
    /// its progression survive the leave — only the party slot is dropped.
    /// </summary>
    [EventKey("recruit_left")]
    public class RecruitLeftEvent : ModEvent
    {
        /// <summary>Instance id of the recruit.</summary>
        public string RecruitId { get; init; }
        /// <summary>Character template id the recruit was spawned from.</summary>
        public string RecruitName { get; init; }
    }
}
