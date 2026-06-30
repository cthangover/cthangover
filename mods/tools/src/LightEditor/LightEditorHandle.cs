using Cthangover.Core.UI.Lights;
using Godot;

namespace Cthangover.Core.UI.Tool.LightEditor
{
    /// <summary>
    /// Draggable 24×24 coloured handle representing a light source on the preview.
    /// Overrides <c>_GuiInput</c> (Godot virtual) for mouse drag support; the colour
    /// is taken from <see cref="LightDef.ToColor"/>. Raises <see cref="DragUpdate"/>
    /// with the handle's centre position in global coordinates for the window to
    /// convert back to normalised coordinates.
    /// </summary>
    public partial class LightEditorHandle : ColorRect
    {
        /// <summary>Index of the light in <see cref="LightEditorController.Lights"/>.</summary>
        public int LightIndex;
        /// <summary>Invoked during drag with the centre position in global coordinates.</summary>
        public System.Action<Vector2> DragUpdate;
        /// <summary>Invoked when the handle is clicked (mouse down).</summary>
        public System.Action Clicked;

		private bool dragging;
		private Vector2 dragOffset;

        /// <summary>Creates a 24×24 handle coloured to match the light's <see cref="LightDef.ToColor"/> value.</summary>
        public LightEditorHandle(LightDef light, int index)
		{
			LightIndex = index;
			Size = new Vector2(24, 24);
			Color = light.ToColor();
			MouseFilter = MouseFilterEnum.Stop;
		}

        /// <summary>Handles mouse drag and click via Godot's virtual input system (<c>_GuiInput</c>).</summary>
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
