using Cthangover.Core.Settings;
using Cthangover.Core.UI.Event;
using Godot;

namespace Cthangover.Mods.Time
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

		/// <summary>Finds the clock label, subscribes to timer ticks and renders the initial time.</summary>
		public override void _Ready()
		{
			timerField = GetNodeOrNull<Label>("TimeLabel");
			eventController = GetTree()?.Root?.GetNodeOrNull<SceneEventController>("EventController")
						  ?? GetNodeOrNull<SceneEventController>("/root/EventController");
			eventController?.AddTimerTickEventListener(this);
			OnTimerTick();
		}

		/// <summary>Detaches from the timer ticks when the clock leaves the scene.</summary>
		public override void _ExitTree()
		{
			eventController?.RemoveTimerTickEventListener(this);
		}

		/// <summary>
		/// Priority in the <see cref="SceneEventController"/> listener chain. Returns 0 (default).
		/// </summary>
		public int Priority => 0;

		/// <summary>
		/// Called by <see cref="SceneEventController"/> on each timer tick. Delegates to
		/// <see cref="UpdateRenderedTime"/> to refresh the on-screen clock label.
		/// </summary>
		public void OnTimerTick()
		{
			UpdateRenderedTime();
		}

		/// <summary>
		/// Applies <c>timeScale</c> to <c>Engine.TimeScale</c> (global time speed), reads the
		/// current in-game time from <see cref="GameData.Runtime.Time"/>, and writes its text
		/// representation to the <c>TimeLabel</c> node.
		/// </summary>
		public void UpdateRenderedTime()
		{
			Godot.Engine.TimeScale = timeScale;
			var time = GameData.Instance.Runtime.Time;
			var text = time?.Text ?? string.Empty;
			if (timerField != null)
				timerField.Text = text;
		}

#if TOOLS
		/// <summary>Refreshes the live time preview whenever an exported property changes in the editor inspector.</summary>
		public override void _ValidateProperty(Godot.Collections.Dictionary property)
		{
			base._ValidateProperty(property);
			OnTimerTick();
		}
#endif

	}

}
