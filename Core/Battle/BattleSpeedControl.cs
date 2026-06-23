using System.Collections.Generic;
using Cthangover.Core.Settings;
using Godot;

namespace Cthangover.Core.Battle
{
	public partial class BattleSpeedControl : Control
	{
		private readonly Dictionary<int, Button> _buttons = new();
		private int _activeSpeed;

		public override void _Ready()
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

			var viewportSize = GetViewport().GetVisibleRect().Size;
			Position = new Vector2(viewportSize.X - 200, 20);

			ApplySavedSpeed();
		}

		public void ApplySavedSpeed()
		{
			var speed = GameData.Instance.Settings.BattleSpeed;
			if (speed < 1 || speed > 4)
				speed = 1;

			_activeSpeed = speed;
			Engine.TimeScale = speed;
			UpdateHighlight();
		}

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
