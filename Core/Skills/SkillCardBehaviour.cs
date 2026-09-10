using Cthangover.Core.UI.Base.Lists;
using Godot;

namespace Cthangover.Core.Skills
{
	/// <summary>
	/// Godot <c>Control</c> node that renders a single skill card inside a list
	/// widget. It extends the generic <see cref="ListItem{T}"/> base, which
	/// handles pooling and lifecycle. On <see cref="Construct(SkillInfo)"/> it
	/// binds the card's artwork, translates the display name, and initialises
	/// the rarity-coloured frame via <see cref="SkillCardFrameBehaviour.Init"/>.
	/// On <see cref="Destruct"/> it frees the node back to the object pool.
	/// </summary>
	public partial class SkillCardBehaviour : ListItem<SkillInfo>
	{
		[Export] private SkillCardFrameBehaviour frame;
		[Export] private Label txtName;
		[Export] private TextureRect imgCard;

		/// <summary>
		/// Called when this list item is recycled or first shown. Applies the
		/// <see cref="SkillInfo"/> data to child nodes: sets the sprite texture,
		/// the localised display name, and the card frame's rarity colour.
		/// </summary>
		/// <param name="skillInfo">
		/// The skill definition to render. Must be non-null and fully populated.
		/// </param>
		public override void Construct(SkillInfo skillInfo)
		{
			base.Construct(skillInfo);

			if (imgCard == null)
				imgCard = GetNodeOrNull<TextureRect>("ImgCard");
			if (txtName == null)
				txtName = GetNodeOrNull<Label>("TxtName");
			if (frame == null)
				frame = GetNodeOrNull<SkillCardFrameBehaviour>("Frame");

			if (imgCard != null)
				imgCard.Texture = skillInfo.Sprite;
			if (txtName != null)
				txtName.Text = TranslationServer.Translate(skillInfo.Name);
			if (frame != null)
				frame.Init(skillInfo.RareType);
		}

		/// <summary>
		/// Frees this node via <see cref="Node.QueueFree"/>, returning it to the
		/// Godot scene tree pool. Called when the list item is no longer visible
		/// or the widget is disposed.
		/// </summary>
		public override void Destruct()
		{
			QueueFree();
		}
	}
}
