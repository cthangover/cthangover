using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Lights
{
    public partial class LampBehaviour : TextureRect
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
            MouseFilter = MouseFilterEnum.Stop;
            controller = SceneContextNode.FindNode<UiLightController>("Lights");

            GameLogger.Log("LIGHT", $"Lamp._Ready: controller={(controller != null ? "OK" : "NULL")}", LogLevel.Debug);

            if (Texture == null)
                Texture = UIIconFactory.Instance.Get("lamp");

            GameLogger.Log("LIGHT", $"Lamp._Ready: Texture={(Texture != null ? "OK" : "NULL")}", LogLevel.Debug);
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