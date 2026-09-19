#if TOOLS
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
	/// <summary>
	/// Automated smoke run of the main menu: waits for the menu to settle,
	/// clicks "New Game", then attaches a <see cref="DialogAutoDriver"/> to
	/// the tree root so it survives the scene change and auto-advances the
	/// opening scenario until it ends (exit code 0). A watchdog quits with
	/// exit code 1 if no dialog is completed within <c>--timeout=</c>
	/// seconds (default 120), so the run can never hang.
	/// </summary>
	public partial class MainMenuTestSetup : Node
	{
		private const float ClickDelaySeconds = 0.5f;
		private const float DriverAttachDelaySeconds = 1.0f;
		private const float DefaultTimeoutSeconds = 120f;

		private float _timer = ClickDelaySeconds;
		private float _timeoutSeconds = DefaultTimeoutSeconds;
		private bool _enabled;
		private bool _started;

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
				}
				else if (arg.StartsWith("--timeout=") && float.TryParse(arg.Substring("--timeout=".Length), out var t) && t > 0)
				{
					_timeoutSeconds = t;
				}
			}
		}

		public override void _Process(double delta)
		{
			if (!_enabled || _started)
				return;

			_timer -= (float)delta;
			if (_timer > 0)
				return;

			SetProcess(false);

			var newGameBtn = GetParent().GetNodeOrNull<Button>("MenuContainer/NewGameBtn");
			if (newGameBtn == null)
			{
				GameLogger.Log("TEST", "MainMenuTestSetup: NewGameBtn not found, quitting", LogLevel.Error);
				GameLogger.Log("TEST", "[TEST_RESULT] name=menu passed=0 failed=1");
				GetTree().Quit(1);
				return;
			}

			_started = true;
			var tree = GetTree();

			tree.CreateTimer(DriverAttachDelaySeconds).Timeout += () =>
			{
				var driver = new DialogAutoDriver { ResultName = "menu" };
				tree.Root.AddChild(driver);
			};
			tree.CreateTimer(_timeoutSeconds).Timeout += () =>
			{
				GameLogger.Log("TEST", $"MainMenuTestSetup: timeout ({_timeoutSeconds}s) reached without dialog end, quitting", LogLevel.Error);
				GameLogger.Log("TEST", "[TEST_RESULT] name=menu passed=0 failed=1");
				tree.Quit(1);
			};

			GameLogger.Log("TEST", "MainMenuTestSetup: clicking 'New Game' button");
			newGameBtn.EmitSignal("pressed");
		}
	}
}
#endif
