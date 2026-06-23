using System.Collections.Generic;

namespace Cthangover.Core.Items
{
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
