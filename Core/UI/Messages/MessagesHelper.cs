using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Messages
{
    /// <summary>
    /// Static factory for floating UI messages (like "Quest updated" or item
    /// pickup notifications). Manages a cycling position index of 0-5 so
    /// consecutive messages stack vertically without overlapping. Each message
    /// is instantiated from Message.tscn and parented to the Background node
    /// (found via scene tree search). Messages auto-fade and self-destruct
    /// via UiMessage's OnUpdate.
    /// </summary>
    public static class MessagesHelper
    {
        private const int maxMessageIndex = 5;
        private static int lastMessageIndex = 0;

        private static PackedScene prefab;

        private static int GetNextIndex(int index)
        {
            if (index++ >= maxMessageIndex)
                index = 0;
            return index;
        }

		private static float GetPositionByLastIndex(int index)
		{
			return (index - 1) * 60;
		}

        /// <summary>
        /// Resets the cycling position index to 0. Call when UI state resets
        /// to prevent messages from accumulating at high Y positions.
        /// </summary>
        public static void ClearLastIndex()
        {
            lastMessageIndex = 0;
        }

        /// <summary>
        /// Shows a yellow message at the next vertical slot. Convenience overload
        /// using the default warning/accent color.
        /// </summary>
        /// <param name="text">The message text to display.</param>
        /// <param name="speed">Fade speed multiplier (higher = faster fade, default 1).</param>
        public static void AddMessage(string text, float speed = 1f)
        {
            AddMessage(text, Colors.Yellow, speed);
        }


        /// <summary>
        /// Shows a message with a custom color at the next vertical slot.
        /// Messages auto-fade and self-destruct after ~3 seconds of display plus
        /// a fade duration determined by <paramref name="speed"/>.
        /// </summary>
        /// <param name="text">The message text to display.</param>
        /// <param name="color">The text color.</param>
        /// <param name="speed">Fade speed multiplier (higher = faster fade, default 1).</param>
        public static void AddMessage(string text, Color color, float speed = 1f)
        {
            var nextIndex = GetNextIndex(lastMessageIndex);
            AddMessage(GetPositionByLastIndex(nextIndex), text, color, speed);
            lastMessageIndex = nextIndex;
        }


        /// <summary>
        /// Shows a yellow message at a specific vertical position, bypassing
        /// the cycling slot system.
        /// </summary>
        /// <param name="position">Y-offset in pixels for this message.</param>
        /// <param name="text">The message text to display.</param>
        /// <param name="speed">Fade speed multiplier (default 1).</param>
        public static void AddMessage(float position, string text, float speed = 1f)
        {
            AddMessage(position, text, Colors.Yellow, speed);
        }

        /// <summary>
        /// Instantiates a <see cref="UiMessage"/> from <c>Message.tscn</c>, parents
        /// it to the Background node, and configures it for display and auto-fade.
        /// </summary>
        /// <param name="position">Y-offset in pixels for this message.</param>
        /// <param name="text">The message text to display.</param>
        /// <param name="color">The text color.</param>
        /// <param name="speed">Fade speed multiplier (default 1).</param>
        public static void AddMessage(float position, string text, Color color, float speed = 1f)
        {
            if (prefab == null)
                prefab = GD.Load<PackedScene>("res://scenes/ui/message.tscn");

            var instance = prefab.Instantiate<UiMessage>();
            var background = SceneContextNode.FindNode<Control>("Background");
            if (background == null)
            {
                background = SceneContextNode.Instance?.GetTree()?.GetFirstNodeInGroup("Background") as Control;
                if (background == null)
                {
                    GameLogger.Log("UI", "Background node not found for messages", LogLevel.Error);
                    return;
                }
            }
            background.AddChild(instance);
            instance.Setup(position, text, speed, color);
        }

    }

}
