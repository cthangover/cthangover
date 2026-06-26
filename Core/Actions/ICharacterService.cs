namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Character manipulation contract for scenario actions. AddToParty recruits
    /// a character by CharacterType enum name; SendNotification displays a UI
    /// notification that a character has joined.
    /// </summary>
    public interface ICharacterService
    {
        void AddToParty(string type);
        void SendNotification(string type);
    }
}