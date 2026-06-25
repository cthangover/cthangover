namespace Cthangover.Core.Actions
{
    public interface ICharacterService
    {
        void AddToParty(string type);
        void SendNotification(string type);
    }
}