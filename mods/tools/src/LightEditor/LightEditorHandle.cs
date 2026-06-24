using Cthangover.Core.UI.Lights;
using Godot;

namespace Cthangover.Core.UI.Tool.LightEditor
{
	public partial class LightEditorHandle : ColorRect
	{
		public int LightIndex;
		public System.Action<Vector2> DragUpdate;
		public System.Action Clicked;

		private bool dragging;
		private Vector2 dragOffset;

		public LightEditorHandle(LightDef light, int index)
		{
			LightIndex = index;
			Size = new Vector2(24, 24);
			Color = light.ToColor();
			MouseFilter = MouseFilterEnum.Stop;
		}

		public override void _GuiInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
			{
				if (mb.Pressed)
				{
					dragging = true;
					dragOffset = GetGlobalMousePosition() - GlobalPosition;
					Clicked?.Invoke();
				}
				else
				{
					dragging = false;
				}
				AcceptEvent();
			}
			else if (@event is InputEventMouseMotion && dragging)
			{
				GlobalPosition = GetGlobalMousePosition() - dragOffset;
				DragUpdate?.Invoke(GlobalPosition + Size / 2);
			}
		}
	}
}
