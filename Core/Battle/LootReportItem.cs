using Godot;

namespace Cthangover.Core.Battle
{
    /// <summary>
    /// A single loot cell in the victory grid. Shows icon with an optional
    /// "xN" count label (hidden when count is 1). ResolveMissingNodes
    /// supports both editor-assigned exports and runtime lookups so the
    /// prefab works with incomplete scene wiring. TooltipText is set to
    /// the localized item name for hover.
    /// </summary>
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

        /// <summary>
        /// Configures the loot cell: sets the item icon, a "xN" count
        /// label (hidden when <paramref name="count"/> is 1), and the
        /// localised name as the tooltip. Calls
        /// <c>ResolveMissingNodes</c> to handle missing editor
        /// assignments gracefully.
        /// </summary>
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
