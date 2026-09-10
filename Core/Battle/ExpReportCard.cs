using Godot;

namespace Cthangover.Core.Battle
{
    /// <summary>
    /// Single character EXP report in the victory screen. Displays the
    /// character icon, localized name, and "+N" EXP value. Uses the same
    /// ResolveMissingNodes pattern as LootReportItem for robustness
    /// against missing scene links.
    /// </summary>
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

        /// <summary>
        /// Populates the card with a character icon, localised name, and
        /// "+N" EXP value. Calls <c>ResolveMissingNodes</c> to tolerate
        /// incomplete scene wiring in the editor.
        /// </summary>
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
