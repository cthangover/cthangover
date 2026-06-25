namespace Cthangover.Core.Items
{
    public interface IItemAction
    {
        string ID { get; }
        bool UseAction(IItem item);
    }
}
