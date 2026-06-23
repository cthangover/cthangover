using Godot;

namespace Cthangover.Core.UI.Dialog
{

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
