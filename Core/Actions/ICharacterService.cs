namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Character manipulation contract for scenario actions. AddToParty recruits
    /// a character by its string ID; SendNotification displays a UI notification
    /// that a character has joined.
    /// </summary>
    public interface ICharacterService
    {
        /// <summary>
        /// Adds a character to the player's party roster. The type string is
        /// a character ID — it is passed directly to CharacterData.AddCharacterToParty
        /// which handles roster mutation, persistence, and fallback for unknown IDs
        /// (a minimal CharacterInfoData is created with default attributes when
        /// no template is found).
        /// </summary>
        void AddToParty(string type);

        /// <summary>
        /// Displays a "character joined" UI notification without modifying
        /// the party roster. Used when recruitment happens through an
        /// external mechanism but the player still needs visual feedback,
        /// or when replaying a notification for an already-recruited
        /// character. The type string is a character ID passed directly
        /// to CharacterData.SendAddNotification.
        /// </summary>
        void SendNotification(string type);
    }
}
