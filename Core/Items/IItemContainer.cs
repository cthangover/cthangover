namespace Cthangover.Core.Items
{
    /// <summary>
    /// A mutable inventory slot: an item reference paired with a stack
    /// count. The settable <c>Count</c> supports stacking and splitting
    /// without recreating the container object. Distinct from
    /// <c>IIngredient</c> because the game treats "what a recipe needs"
    /// and "what the player holds" as separate concerns — recipe
    /// validation should not accidentally modify inventory state.
    /// </summary>
    public interface IItemContainer
    {
        IItem Item  { get; set; }
        int   Count { get; set; }
    }

}
