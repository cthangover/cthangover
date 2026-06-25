

namespace Cthangover.Core.Items
{
    public interface IIngredient {
        IItem Item  { get; set; }
        int   Count { get; set; }
    }
}
