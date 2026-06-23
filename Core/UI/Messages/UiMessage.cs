using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Event;
using Godot;

namespace Cthangover.Core.UI.Messages
{

    public partial class UiMessage : Control, IOnUpdateEvent
    {

        [Export] private Label textField;
        [Export] public  Color Color { get; set; } = Colors.Yellow;
        [Export] public  float Speed { get; set; } = 1f;
        private         float timestamp;

        public string Text
        {
            get => textField.Text;
            set => textField.Text = value;
        }

		public void Setup(float pos, string text, float speed, Color color)
		{
			OffsetLeft = 50;
			OffsetRight = -10;
			Size = new Vector2(Size.X, 50);
			Position = new Vector2(0, pos + 200);

			Text = text;
			Speed = speed;
			Color = color;
			timestamp = (float)Time.GetTicksMsec() / 1000f;

			if (SceneContextNode.Instance != null)
			{
				var eventController = SceneContextNode.FindNode<SceneEventController>("EventController");
				eventController?.AddUpdateEventListener(this);
			}
		}

        public override void _ExitTree()
        {
            if (SceneContextNode.Instance != null)
            {
                var eventController = SceneContextNode.FindNode<SceneEventController>("EventController");
                eventController?.RemoveUpdateEventListener(this);
            }
        }

        public int Priority => 1;
		public void OnUpdate()
		{
			var currentTime = (float)Time.GetTicksMsec() / 1000f;
			var elapsed = currentTime - timestamp;

			if (elapsed < 3f)
				return;

			var fadeDuration = 2f / Speed;
			var fadeElapsed = elapsed - 3f;
			var progress = Mathf.Clamp(fadeElapsed / fadeDuration, 0f, 1f);
			textField.SelfModulate = new Color(1, 1, 1, 1f - progress);

			if (progress >= 1f)
				QueueFree();
		}

    }

}
