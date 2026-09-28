namespace Cthangover.Core.Events
{
    /// <summary>
    /// Published when a group of characters is defeated as one encounter outcome —
    /// a won battle, a resolved trap, a scripted execution. The core stays neutral
    /// about who defeated them: <see cref="Source"/> names the reporting system
    /// (<c>"battle"</c>, <c>"trap"</c>, …) and interested mods subscribe without
    /// depending on the publisher.
    /// <para/>
    /// The payload carries ids only rather than live <c>Character</c> objects so the
    /// event bridges to GDScript safely (the string bridge cannot marshal arbitrary
    /// managed collections). Subscribers that need full character data resolve it
    /// through <c>CharacterFactory</c>.
    /// </summary>
    [EventKey("characters_defeated")]
    public class CharactersDefeatedEvent : ModEvent
    {
        /// <summary>Free-form id of the system that reported the defeat.</summary>
        public string Source { get; init; }

        /// <summary>Template ids of the defeated characters.</summary>
        public string[] CharacterIds { get; init; }
    }
}
