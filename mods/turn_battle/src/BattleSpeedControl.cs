using System.Collections.Generic;
using Cthangover.Core.Battle;
using Cthangover.Core.Settings;
using Godot;

namespace Cthangover.TurnBattle
{
    /// <summary>
    /// In-battle speed toggle bar (x1/x4/x8). Directly sets
    /// Engine.TimeScale to globally accelerate the entire game loop —
    /// not just animations. Persists the choice to GameData.Settings
    /// so it survives scene reloads. <see cref="ResetToNormal"/> restores 1x
    /// without saving and is the part the core scene consumes through
    /// <see cref="IBattleSpeedControl"/> when battle ends.
    /// </summary>
	public partial class BattleSpeedControl : Control, IBattleSpeedControl
	{
		private readonly Dictionary<int, Button> _buttons = new();
		private int _activeSpeed;

		/// <summary>
		/// Builds the speed bar in the constructor. Godot never runs a
		/// <c>_Ready</c> override declared in a mod assembly, so the buttons and the
		/// saved-speed restore must happen here. Anchors are used instead of a
		/// viewport-size lookup because the viewport is unavailable before the node
		/// enters the tree.
		/// </summary>
		public BattleSpeedControl()
		{
			var speeds = new[] { 1, 4, 8 };
			var hbox = new HBoxContainer { Name = "SpeedButtons" };
			hbox.Alignment = BoxContainer.AlignmentMode.Begin;

			foreach (var speed in speeds)
			{
				var btn = new Button
				{
					Text = $"x{speed}",
					CustomMinimumSize = new Vector2(48, 32)
				};
				btn.Pressed += () => OnSpeedSelected(speed);
				hbox.AddChild(btn);
				_buttons[speed] = btn;
			}

			AddChild(hbox);

			AnchorLeft = 1f;
			AnchorRight = 1f;
			AnchorTop = 0f;
			AnchorBottom = 0f;
			OffsetLeft = -200f;
			OffsetRight = -20f;
			OffsetTop = 20f;
			OffsetBottom = 52f;

			ApplySavedSpeed();
		}

		/// <summary>
		/// Restores the speed from <see cref="GameData.Settings.BattleSpeed"/>.
		/// Called on <c>_Ready</c> to reapply the saved preference.
		/// </summary>
		public void ApplySavedSpeed()
		{
			var speed = GameData.Instance.Settings.BattleSpeed;
			if (speed < 1 || speed > 8)
				speed = 1;

			_activeSpeed = speed;
			Engine.TimeScale = speed;
			UpdateHighlight();
		}

		/// <summary>
		/// Resets <see cref="Engine.TimeScale"/> to 1.0x without
		/// persisting to settings. Used when battle ends so the lobby
		/// and menus run at normal speed regardless of the player's
		/// in-battle preference.
		/// </summary>
		public void ResetToNormal()
		{
			Engine.TimeScale = 1.0f;
		}

		private void OnSpeedSelected(int speed)
		{
			_activeSpeed = speed;
			Engine.TimeScale = speed;

			var settings = GameData.Instance.Settings;
			settings.BattleSpeed = speed;
			settings.Save();

			UpdateHighlight();
		}

		private void UpdateHighlight()
		{
			foreach (var kv in _buttons)
			{
				var isActive = kv.Key == _activeSpeed;
				kv.Value.Modulate = isActive ? new Color(0.4f, 1f, 0.4f, 1f) : Colors.White;
			}
		}
	}
}
