namespace Cthangover.Core.Events
{
    /// <summary>
    /// Published when a character dies in combat. Payload copies data
    /// as strings — does not pass a live Node (may be freed by the time
    /// deferred subscribers are called).
    /// </summary>
    [EventKey("character_died")]
    public class CharacterDiedEvent : ModEvent
    {
        public string CharacterId { get; init; }
        public string CharacterName { get; init; }
        public string Source { get; init; }
    }
}
