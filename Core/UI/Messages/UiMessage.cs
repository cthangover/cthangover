using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Event;
using Godot;

namespace Cthangover.Core.UI.Messages
{
    /// <summary>
    /// Floating text message with auto-fade. Appears at a vertical position,
    /// stays visible for 3 seconds, then fades over 2/Speed seconds. Self-
    /// destructs (QueueFree) when fully transparent. Registers with
    /// SceneEventController for per-frame updates; unregisters on tree exit
    /// to prevent leaks. Speed affects only the fade duration, not the display
    /// duration, so faster speed = shorter fade = message disappears more quickly.
    /// </summary>
    public partial class UiMessage : Control, IOnUpdateEvent
    {

        [Export] private Label textField;
        [Export] public  Color Color { get; set; } = Colors.Yellow;
        [Export] public  float Speed { get; set; } = 1f;
        private         float timestamp;

        /// <summary>
        /// The text content displayed by this message. Reads/writes the child
        /// <c>textField</c> label directly.
        /// </summary>
        public string Text
        {
            get => textField.Text;
            set => textField.Text = value;
        }

        /// <summary>
        /// Initializes the message: positions it, sets text/speed/color, records
        /// the start timestamp, and registers with the <see cref="SceneEventController"/>
        /// for per-frame updates. Called by <see cref="MessagesHelper"/>.
        /// </summary>
        /// <param name="pos">Vertical Y-offset in pixels.</param>
        /// <param name="text">The message text.</param>
        /// <param name="speed">Fade speed multiplier.</param>
        /// <param name="color">The text color.</param>
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

        /// <summary>
        /// Priority <c>1</c> — lightweight enough for many concurrent messages.
        /// </summary>
        public int Priority => 1;

        /// <summary>
        /// Per-frame update: displays for 3 seconds, then fades out over
        /// <c>2 / <see cref="Speed"/></c> seconds. Self-destructs via
        /// <c>QueueFree</c> when fully transparent.
        /// </summary>
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
