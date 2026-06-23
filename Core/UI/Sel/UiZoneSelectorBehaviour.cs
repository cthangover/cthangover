using System.Linq;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Event;
using Cthangover.Core.UI.Executable;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Sel
{

    public partial class UiZoneSelectorBehaviour : Control, IOnUpdateEvent
    {

        [Export] private Color selectedColor = new Color(1f, 0.8f, 0, 0.25f);
        [Export] private Control image;
        [Export] private ExecutableEvent executableEvent;
        [Export] private string executableEventID;

        [Export] private float speed = 0.15f;

        private Color color = Colors.Transparent;
        private float timestamp;

        public override void _Ready()
        {
            image.Modulate = Colors.Transparent;

            if (SceneContextNode.Instance != null)
            {
                var eventController = SceneContextNode.FindNode<SceneEventController>("EventController");
                eventController?.AddUpdateEventListener(this);
            }

            GuiInput += OnGuiInput;
            MouseEntered += OnMouseEntered;
            MouseExited += OnMouseExited;
        }

        public override void _ExitTree()
        {
            if (SceneContextNode.Instance != null)
            {
                var eventController = SceneContextNode.FindNode<SceneEventController>("EventController");
                eventController?.RemoveUpdateEventListener(this);
            }
        }

        private void OnGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButton && mouseButton.Pressed && mouseButton.ButtonIndex == MouseButton.Left)
            {
                OnPointerClick();
            }
        }

        public void OnPointerClick()
        {
            if (executableEvent == null)
            {
                var tree = GetTree();
                if (tree != null)
                {
                    var allNodes = new Godot.Collections.Array<Node>();
                    FindChildrenRecursive(tree.Root, allNodes);
                    executableEvent = allNodes.OfType<ExecutableEvent>().FirstOrDefault(o => o.ID == executableEventID);
                }
            }

            if (executableEvent == null)
            {
                GameLogger.Log("UI", "event '" + executableEventID + "' is null!", LogLevel.Error);
                return;
            }

            executableEvent?.RunDialog();
        }

        private static void FindChildrenRecursive(Node node, Godot.Collections.Array<Node> results)
        {
            foreach (Node child in node.GetChildren())
            {
                results.Add(child);
                FindChildrenRecursive(child, results);
            }
        }

        public void OnMouseEntered()
        {
            timestamp = (float)Time.GetTicksMsec() / 1000f;
            color = selectedColor;
        }

        public void OnMouseExited()
        {
            timestamp = (float)Time.GetTicksMsec() / 1000f;
            color = Colors.Transparent;
        }

        public int Priority { get; } = 1;
        public void OnUpdate()
        {
            var currentTime = (float)Time.GetTicksMsec() / 1000f;
            var time = Mathf.Min((currentTime - timestamp) * speed, 1f);
            image.Modulate = new Color(
                Mathf.Lerp(image.Modulate.R, color.R, time),
                Mathf.Lerp(image.Modulate.G, color.G, time),
                Mathf.Lerp(image.Modulate.B, color.B, time),
                Mathf.Lerp(image.Modulate.A, color.A, time)
            );
        }

    }

}
