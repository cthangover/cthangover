using Cthangover.Core.Factories.Impls;
using Godot;

namespace Cthangover.Core.UI.Base
{
	public partial class ModSpriteLoader : Node
	{
		[Export] public string TextureId { get; set; }

		public override void _Ready()
		{
			if (string.IsNullOrEmpty(TextureId))
				return;

			var texture = UITextureFactory.Instance.Get(TextureId);
			if (texture != null && GetParent() is TextureRect sprite)
				sprite.Texture = texture;
		}
	}
}
