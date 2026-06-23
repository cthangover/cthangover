namespace Cthangover.Core.Items
{

    public interface IItemContainer
    {
        IItem Item  { get; set; }
        int   Count { get; set; }
    }

}
