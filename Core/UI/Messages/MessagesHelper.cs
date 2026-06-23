using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Messages
{

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

        public static void ClearLastIndex()
        {
            lastMessageIndex = 0;
        }

        public static void AddMessage(string text, float speed = 1f)
        {
            AddMessage(text, Colors.Yellow, speed);
        }


        public static void AddMessage(string text, Color color, float speed = 1f)
        {
            var nextIndex = GetNextIndex(lastMessageIndex);
            AddMessage(GetPositionByLastIndex(nextIndex), text, color, speed);
            lastMessageIndex = nextIndex;
        }


        public static void AddMessage(float position, string text, float speed = 1f)
        {
            AddMessage(position, text, Colors.Yellow, speed);
        }

        public static void AddMessage(float position, string text, Color color, float speed = 1f)
        {
            if (prefab == null)
                prefab = GD.Load<PackedScene>("res://scenes/ui/Message.tscn");

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
