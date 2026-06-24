using Cthangover.Core.Factories.Impls;
using Godot;

namespace Cthangover.Core.UI.Tool
{
	public partial class ButtonModIconLoader : TextureButton
	{
		[Export] public string IconId { get; set; }

		public override void _Ready()
		{
			if (string.IsNullOrEmpty(IconId))
				return;

			var texture = UIIconFactory.Instance.Get(IconId);
			if (texture != null)
				TextureNormal = texture;
		}
	}
}
