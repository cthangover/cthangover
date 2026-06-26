using Cthangover.Core.Factories.Impls;
using Godot;

namespace Cthangover.Core.UI.Tool
{
    /// <summary>
    /// Mod-aware TextureButton: resolves IconId through UIIconFactory at _Ready
    /// and applies the result to TextureNormal. Allows tool buttons to reference
    /// icons by logical ID that mods can override.
    /// </summary>
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
