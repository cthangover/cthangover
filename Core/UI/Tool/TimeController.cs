using Cthangover.Core.Settings;
using Cthangover.Core.UI.Event;
using Godot;

namespace Cthangover.Core.UI.Tool
{
    /// <summary>
    /// In-game clock: subscribes to SceneEventController timer ticks and advances
    /// GameData.Runtime.Time. Renders the time text on a Label and applies
    /// Godot.Engine.TimeScale from the exported timeScale field for global
    /// time acceleration. The #if TOOLS block updates the rendered time in the
    /// editor inspector when properties change, giving designers live time preview.
    /// </summary>
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
			OnTimerTick();
		}

		public override void _ExitTree()
		{
			eventController?.RemoveTimerTickEventListener(this);
		}

		public int Priority => 0;

		public void OnTimerTick()
		{
			UpdateRenderedTime();
		}

		public void UpdateRenderedTime()
		{
			Engine.TimeScale = timeScale;
			var time = GameData.Instance.Runtime.Time;
			var text = time?.Text ?? string.Empty;
			if (timerField != null)
				timerField.Text = text;
		}

#if TOOLS
		public override void _ValidateProperty(Godot.Collections.Dictionary property)
		{
			base._ValidateProperty(property);
			OnTimerTick();
		}
#endif

	}

}
