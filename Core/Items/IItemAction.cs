namespace Cthangover.Core.Items
{
    /// <summary>
    /// Contract for an item's on-use behaviour. <c>UseAction</c> returns
    /// <c>false</c> when the action cannot be performed (e.g. using a
    /// healing item at full health), letting the UI play a failure
    /// animation or show a tooltip rather than silently consuming the
    /// item. The <c>IItem</c> parameter passes the item definition to
    /// the action so the behaviour can read item properties without
    /// owning a reference — this allows a single action class to serve
    /// multiple items with different magnitudes (e.g. a generic
    /// "HealAction" where the amount comes from the item's data).
    /// </summary>
    public interface IItemAction
    {
        string ID { get; }
        bool UseAction(IItem item);
    }
}
