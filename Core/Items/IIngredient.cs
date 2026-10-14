

namespace Cthangover.Core.Items
{
    /// <summary>
    /// A quantity of an item used as a recipe input or output. Structurally
    /// identical to <c>IItemContainer</c> but intentionally kept as a
    /// separate interface — ingredients represent a recipe <b>formula</b>
    /// (what is required or produced), while containers represent an
    /// <b>inventory slot</b> (what is currently held). This distinction
    /// allows the type system to prevent accidentally treating a recipe
    /// ingredient as a mutable inventory entry.
    /// </summary>
    public interface IIngredient {
        /// <summary>The resolved item this formula entry references.</summary>
        IItem Item  { get; set; }
        /// <summary>How many copies the recipe consumes or produces.</summary>
        int   Count { get; set; }
    }
}
