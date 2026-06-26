namespace Cthangover.Core.Items
{
    /// <summary>
    /// Crafting station type. Currently only <c>Cooking</c> exists,
    /// suggesting the crafting system was designed with multiple
    /// workbench types in mind (workshop, alchemy, forge) but the
    /// implementation started with cooking as the first — and so far
    /// only — station. Recipes specify their required bench via
    /// <c>RecipeInfo.Type</c>, and the crafting UI filters available
    /// recipes by the currently opened station.
    /// </summary>
    public enum WorkbenchType
    {
        Cooking,
    }
}
