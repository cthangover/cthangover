using Godot;

namespace Cthangover.Core.UI.Dialog
{
    /// <summary>
    /// Thin wrapper node that locates its DialogBox child for convenient access.
    /// Exists primarily so the scene tree root can reference the DialogBox through
    /// this named node without hardcoding child paths.
    /// </summary>
	public partial class DialogCanvas : Control
	{
		private DialogBox dialogBox;

		public DialogBox DialogBox => dialogBox;

		public override void _Ready()
		{
			dialogBox ??= GetNodeOrNull<DialogBox>("DialogBox");
		}
	}

}
