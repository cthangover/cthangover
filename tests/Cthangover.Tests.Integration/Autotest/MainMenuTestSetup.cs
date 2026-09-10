#if TOOLS
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
	public partial class MainMenuTestSetup : Node
	{
		private float _timer = 0.5f;
		private bool _enabled;

		public override void _Ready()
		{
			foreach (var arg in OS.GetCmdlineArgs())
			{
				if (arg.StartsWith("--test="))
				{
					var testName = arg.Substring("--test=".Length).ToLower();
					if (testName == "menu")
					{
						_enabled = true;
						GameLogger.Log("TEST", "MainMenuTestSetup: auto-test enabled via --test=menu");
					}
					break;
				}
			}
		}

		public override void _Process(double delta)
		{
			if (!_enabled)
				return;

			_timer -= (float)delta;
			if (_timer > 0)
				return;

			SetProcess(false);

			var newGameBtn = GetParent().GetNodeOrNull<Button>("MenuContainer/NewGameBtn");
			if (newGameBtn != null)
			{
				GameLogger.Log("TEST", "MainMenuTestSetup: clicking 'New Game' button");
				newGameBtn.EmitSignal("pressed");
			}
			else
			{
				GameLogger.Log("TEST", "MainMenuTestSetup: NewGameBtn not found, quitting", LogLevel.Error);
				GetTree().Quit();
			}
		}
	}
}
#endif
