

namespace Cthangover.Core.Items
{
    public class Ingredient : IIngredient{
        public IItem Item  { get; set; }
        public int   Count { get; set; }
    }
}
