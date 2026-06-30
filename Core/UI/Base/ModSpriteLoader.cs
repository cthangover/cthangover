using Cthangover.Core.Factories.Impls;
using Godot;

namespace Cthangover.Core.UI.Base
{
    /// <summary>
    /// Editor-configured texture indirection: resolves a TextureId string through
    /// UITextureFactory and applies the result to the parent TextureRect. This
    /// allows artists to reference textures by logical ID in the Godot inspector
    /// instead of hardcoding paths, and mods can override which texture a given ID
    /// resolves to.
    /// </summary>
    public partial class ModSpriteLoader : Node
	{
		/// <summary>Logical texture identifier resolved through <see cref="UITextureFactory"/>. Set in the Godot inspector.</summary>
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
