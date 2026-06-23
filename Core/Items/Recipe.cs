using System.Collections.Generic;

namespace Cthangover.Core.Items
{
    public class Recipe : IRecipe
    {
        public string           ID            { get; set; }
        public string           Name          { get; set; }
        public string           Description   { get; set; }
        public int              Time          { get; set; }
        public WorkbenchType    WorkbenchType { get; set; }
        public List<IIngredient> Input         { get; set; }
        public List<IIngredient> Output        { get; set; }
    }
}
