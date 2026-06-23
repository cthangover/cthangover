using Godot;

namespace Cthangover.Core.Battle
{
	public partial class ExpReportCard : Control
	{
		[Export] private TextureRect icon;
		[Export] private Label nameLabel;
		[Export] private Label expLabel;

		public override void _Ready()
		{
			ResolveMissingNodes();
			Visible = true;
		}

		private void ResolveMissingNodes()
		{
			icon ??= GetNodeOrNull<TextureRect>("Panel/Icon");
			nameLabel ??= GetNodeOrNull<Label>("Panel/NameLabel");
			expLabel ??= GetNodeOrNull<Label>("Panel/ExpLabel");
		}

		public void Setup(Texture2D characterIcon, string characterName, int exp)
		{
			ResolveMissingNodes();

			if (icon != null && characterIcon != null)
				icon.Texture = characterIcon;

			if (nameLabel != null)
				nameLabel.Text = characterName;

			if (expLabel != null)
				expLabel.Text = "+" + exp;
		}
	}
}
