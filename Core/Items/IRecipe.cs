using System.Collections.Generic;

namespace Cthangover.Core.Items
{
    /// <summary>
    /// Recipe contract for the crafting system. All properties have setters
    /// despite recipes being logically immutable — this is a pragmatic
    /// concession for JSON deserialization rather than enforcing a pure
    /// immutable model. <c>WorkbenchType</c> gates which crafting station
    /// can produce this recipe; <c>Input</c> and <c>Output</c> are lists
    /// of <c>IIngredient</c> rather than raw <c>IItem</c> collections
    /// because a recipe may consume or produce multiple copies of the
    /// same item type.
    /// </summary>
    public interface IRecipe
    {
        string           ID            { get; set; }
        string           Name          { get; set; }
        string           Description   { get; set; }
        int              Time          { get; set; }
        WorkbenchType    WorkbenchType { get; set; }
        List<IIngredient> Input        { get; set; }
        List<IIngredient> Output       { get; set; }
    }
}
