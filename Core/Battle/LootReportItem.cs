using Godot;

namespace Cthangover.Core.Battle
{
	public partial class LootReportItem : Control
	{
		[Export] private TextureRect icon;
		[Export] private Label countLabel;

		public override void _Ready()
		{
			ResolveMissingNodes();
			Visible = true;
		}

		private void ResolveMissingNodes()
		{
			icon ??= GetNodeOrNull<TextureRect>("Panel/Icon");
			countLabel ??= GetNodeOrNull<Label>("Panel/CountLabel");
		}

		public void Setup(Texture2D itemIcon, string localizedName, int count)
		{
			ResolveMissingNodes();

			if (icon != null && itemIcon != null)
				icon.Texture = itemIcon;

			if (countLabel != null)
				countLabel.Text = count > 1 ? "x" + count : "";

			TooltipText = localizedName;
		}
	}
}
