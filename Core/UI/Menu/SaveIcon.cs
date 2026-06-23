using Cthangover.Core.Factories.Impls;
using Godot;

namespace Cthangover.Core.UI.Menu
{
    public partial class SaveIcon : TextureRect
    {
        public override void _Ready()
        {
            if (Texture == null)
                Texture = UIIconFactory.Instance.Get("save");
        }
    }
}