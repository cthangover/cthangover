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
        /// <summary>Index of the vertex this handle represents (0-based).</summary>
		public int VertexIndex;
        /// <summary>Invoked during drag with the centre position of the handle in global coordinates.</summary>
        public System.Action<Vector2> DragUpdate;
        /// <summary>Invoked when the handle is clicked (mouse down).</summary>
        public System.Action Clicked;
        /// <summary>Invoked when the drag ends (mouse up). Used to sync sidebar values.</summary>
        public System.Action DragEnd;

		private bool _dragging;
		private Vector2 _dragOffset;

        /// <summary>Creates a handle with the given colour, size 16×16, and index for vertex identification.</summary>
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
				GlobalPosition = GetGlobalMousePosition() - _dragOffset;
				DragUpdate?.Invoke(center);
			}
		}
	}
}
