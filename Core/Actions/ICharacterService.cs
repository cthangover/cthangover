namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Character manipulation contract for scenario actions. AddToParty recruits
    /// a character by CharacterType enum name; SendNotification displays a UI
    /// notification that a character has joined.
    /// </summary>
    public interface ICharacterService
    {
        /// <summary>
        /// Adds a character to the player's party roster. The type string
        /// must match a CharacterType enum value exactly (case-sensitive
        /// parse via Enums&lt;CharacterType&gt;.Parse). Delegates to
        /// CharacterData.AddCharacterToParty which handles roster mutation
        /// and persistence. Malformed type strings throw at parse time
        /// rather than silently failing — this is intentional to help
        /// scenario authors catch typos during testing.
        /// </summary>
        void AddToParty(string type);

        /// <summary>
        /// Displays a "character joined" UI notification without modifying
        /// the party roster. Used when recruitment happens through an
        /// external mechanism but the player still needs visual feedback,
        /// or when replaying a notification for an already-recruited
        /// character. Reads from CharacterData which tracks which
        /// characters have already been notified to avoid duplicates.
        /// </summary>
        void SendNotification(string type);
    }
}