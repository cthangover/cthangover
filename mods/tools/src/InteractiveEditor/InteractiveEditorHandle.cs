using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Tools.InteractiveEditor
{
	/// <summary>
	/// Draggable 16x16 handle representing a collider vertex on the preview area.
	/// Used for rect corners, circle radius marker, and polygon vertices.
	/// Subscribes to the GuiInput C# event rather than overriding _GuiInput
	/// for reliable event delivery in mod assemblies.
	/// </summary>
	public partial class InteractiveEditorHandle : ColorRect
	{
		public int VertexIndex;
		public System.Action<Vector2> DragUpdate;
		public System.Action Clicked;
		public System.Action DragEnd;

		private bool _dragging;
		private Vector2 _dragOffset;

		public InteractiveEditorHandle(Color color, int index)
		{
			VertexIndex = index;
			Size = new Vector2(16, 16);
			Color = color;
			MouseFilter = Control.MouseFilterEnum.Stop;
			MouseDefaultCursorShape = Control.CursorShape.PointingHand;
			GuiInput += OnGuiInput;
		}

		private void OnGuiInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left)
			{
				if (mb.Pressed)
				{
					_dragging = true;
					_dragOffset = GetGlobalMousePosition() - GlobalPosition;
					GameLogger.Log("INTERACTIVE_HANDLE", $"drag start idx={VertexIndex} pos={GlobalPosition}");
					Clicked?.Invoke();
				}
				else
				{
					_dragging = false;
					GameLogger.Log("INTERACTIVE_HANDLE", $"drag end idx={VertexIndex}");
					DragEnd?.Invoke();
				}
				AcceptEvent();
			}
			else if (@event is InputEventMouseMotion && _dragging)
			{
				var center = GlobalPosition + Size / 2;
				GameLogger.Log("INTERACTIVE_HANDLE", $"drag move idx={VertexIndex} center={center}");
				GlobalPosition = GetGlobalMousePosition() - _dragOffset;
				DragUpdate?.Invoke(center);
			}
		}
	}
}
