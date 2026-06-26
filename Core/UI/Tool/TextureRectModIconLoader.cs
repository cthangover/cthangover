using Cthangover.Core.Factories.Impls;
using Godot;

namespace Cthangover.Core.UI.Tool
{
    /// <summary>
    /// Mod-aware TextureRect: resolves IconId through UIIconFactory at _Ready
    /// and applies the result. Used as a base class for any TextureRect that
    /// needs mod-overridable textures without scene modification.
    /// </summary>
	public partial class TextureRectModIconLoader : TextureRect
	{
		[Export] public string IconId { get; set; }

		public override void _Ready()
		{
			if (string.IsNullOrEmpty(IconId))
				return;

			var texture = UIIconFactory.Instance.Get(IconId);
			if (texture != null)
				Texture = texture;
		}
	}
}
