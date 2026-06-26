using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Tool;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Lights
{
    /// <summary>
    /// Draggable lamp UI element for the player's portable light source.
    /// Input handling exists in both _GuiInput (bubbled) and _Input (global)
    /// to catch drags that start on the lamp but continue outside its rect.
    /// When the lamp is hidden, it resets position to offscreen (-1000,-1000)
    /// to disable the light in the shader. GetLightParams() is a static helper
    /// so the light controller can query lamp parameters without coupling to
    /// the behaviour instance. Inherits from TextureRectModIconLoader for
    /// mod-provided lamp icon textures.
    /// </summary>
	public partial class LampBehaviour : TextureRectModIconLoader
	{
		private UiLightController controller;
		private Vector2 dragPosition;
		private bool isPlaced;

		private bool pressingOnLamp;

		public static (Color, float, float) GetLightParams()
		{
			var lampData = GameData.Instance.Runtime.LampData;
			var radius = lampData.Radius;
			var influence = lampData.Influence;
			return (Colors.Yellow, radius, influence);
		}
		
		public new bool Visible
		{
			get => base.Visible;
			set
			{
				if (base.Visible != value)
					GameLogger.Log("LIGHT", $"Lamp.Visible: {base.Visible} -> {value}, isPlaced={isPlaced}", LogLevel.Debug);
				base.Visible = value;
				if (!value)
				{
					pressingOnLamp = false;
					if (isPlaced)
					{
						isPlaced = false;
						controller?.UpdateLightPos(new Vector2(-1000f, -1000f));
					}
				}
			}
		}

		public override void _Ready()
		{
			base._Ready();
			MouseFilter = MouseFilterEnum.Stop;
			controller = SceneContextNode.FindNode<UiLightController>("Lights");
			GameLogger.Log("LIGHT", $"Lamp._Ready: controller={(controller != null ? "OK" : "NULL")}", LogLevel.Debug);
		}

		public override void _GuiInput(InputEvent @event)
		{
			if (@event is InputEventMouseButton mouseButton)
			{
				if (mouseButton.ButtonIndex == MouseButton.Left)
				{
					if (mouseButton.Pressed)
					{
						pressingOnLamp = true;
						dragPosition = GetGlobalMousePosition();

						if (isPlaced)
						{
							dragPosition = new Vector2(-1000f, -1000f);
							controller?.UpdateLightPos(dragPosition);
							isPlaced = false;
						}
					}
					else
					{
						if (pressingOnLamp && !isPlaced)
						{
							isPlaced = true;
							controller?.UpdateLightPos(dragPosition);
						}
						pressingOnLamp = false;
					}
				}
				AcceptEvent();
			}
			else if (@event is InputEventMouseMotion mouseMotion)
			{
				if (Input.IsMouseButtonPressed(MouseButton.Left))
				{
					dragPosition = GetGlobalMousePosition();
					controller?.UpdateLightPos(dragPosition);
					isPlaced = false;
					AcceptEvent();
				}
			}
		}

		public override void _Input(InputEvent @event)
		{
			if (!Visible)
				return;

			if (@event is InputEventMouseMotion mouseMotion
				&& Input.IsMouseButtonPressed(MouseButton.Left)
				&& pressingOnLamp)
			{
				dragPosition = GetGlobalMousePosition();
				controller?.UpdateLightPos(dragPosition);
				isPlaced = false;
			}

			if (@event is InputEventMouseButton mouseButton
				&& mouseButton.ButtonIndex == MouseButton.Left
				&& !mouseButton.Pressed
				&& pressingOnLamp)
			{
				if (!isPlaced)
				{
					isPlaced = true;
					controller?.UpdateLightPos(dragPosition);
				}
				pressingOnLamp = false;
			}
		}
	}
}
