using Cthangover.Core.Settings;
using Cthangover.Core.UI.Event;
using Godot;

namespace Cthangover.Core.UI.Tool
{

	public partial class TimeController : Control, IOnTimeEvent
	{

		[Export] private float timeScale = 1f;
		private Label timerField;
		private SceneEventController eventController;

		private PhaseType lastPhase;

		public override void _Ready()
		{
			timerField = GetNodeOrNull<Label>("TimeLabel");
			eventController = GetTree()?.Root?.GetNodeOrNull<SceneEventController>("EventController")
						  ?? GetNodeOrNull<SceneEventController>("/root/EventController");
			eventController?.AddTimerTickEventListener(this);
			UpdateRenderedTime();
		}

		public override void _ExitTree()
		{
			eventController?.RemoveTimerTickEventListener(this);
		}

		public int Priority => 0;

		public void OnTimerTick()
		{
			var time = GameData.Instance.Runtime.Time;
			time.AddTick();
			UpdateRenderedTime();
		}

		public void UpdateRenderedTime()
		{
			Godot.Engine.TimeScale = timeScale;
			var time = GameData.Instance.Runtime.Time;
			var text = time?.Text ?? string.Empty;
			if (timerField != null)
				timerField.Text = text;
		}

#if TOOLS
		public override void _ValidateProperty(Godot.Collections.Dictionary property)
		{
			base._ValidateProperty(property);
			UpdateRenderedTime();
		}
#endif

	}

}
