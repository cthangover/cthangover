using Godot;

namespace Cthangover.Tools.InteractiveEditor
{
	/// <summary>
	/// Draggable 16x16 handle representing a collider vertex on the preview area.
	/// Used for rect corners, circle radius marker, and polygon vertices.
	/// </summary>
	public partial class InteractiveEditorHandle : ColorRect
	{
		public int VertexIndex;
		public System.Action<Vector2> DragUpdate;
		public System.Action Clicked;

		private bool _dragging;
		private Vector2 _dragOffset;

		public InteractiveEditorHandle(Color color, int index)
		{
			VertexIndex = index;
			Size = new Vector2(16, 16);
			Color = color;
			MouseFilter = Control.MouseFilterEnum.Stop;
			MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		}

		public override void _GuiInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
			{
				if (mb.Pressed)
				{
					_dragging = true;
					_dragOffset = GetGlobalMousePosition() - GlobalPosition;
					Clicked?.Invoke();
				}
				else
				{
					_dragging = false;
				}
				AcceptEvent();
			}
			else if (@event is InputEventMouseMotion && _dragging)
			{
				GlobalPosition = GetGlobalMousePosition() - _dragOffset;
				DragUpdate?.Invoke(GlobalPosition + Size / 2);
			}
		}
	}
}
