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
        /// <summary>Unique recipe ID (path-style, e.g. <c>"recipe/wolf_meat_to_ration"</c>).</summary>
        string           ID            { get; set; }
        /// <summary>Locale key for the recipe display name.</summary>
        string           Name          { get; set; }
        /// <summary>Locale key for the recipe description text.</summary>
        string           Description   { get; set; }
        /// <summary>Crafting time in in-game minutes.</summary>
        int              Time          { get; set; }
        /// <summary>Required crafting station; the crafting UI filters recipes by the opened bench.</summary>
        WorkbenchType    WorkbenchType { get; set; }
        /// <summary>Ingredients consumed on a successful craft.</summary>
        List<IIngredient> Input        { get; set; }
        /// <summary>Items produced on a successful craft.</summary>
        List<IIngredient> Output       { get; set; }
    }
}
