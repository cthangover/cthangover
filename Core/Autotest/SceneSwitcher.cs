#if TOOLS
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
	/// <summary>
	/// CLI-driven scene launcher that allows AI agents and automated tests to
	/// run any game scene in isolation. Parses <c>--scene=</c> to determine the
	/// target (a .tscn path or a logical scene name), instantiates it, attaches
	/// a <see cref="DialogAutoDriver"/> for dialog auto-progression, and enforces
	/// a configurable timeout to prevent hangs. Designed as the single entry point
	/// for the "edit → build → launch → read logs → iterate" AI workflow.
	/// </summary>
	public partial class SceneSwitcher : Node
	{
		[Export] public float TimeoutSeconds { get; set; } = 60f;

		private string _targetScene;
		private string _choices;
		private float _elapsed;
		private bool _driverAttached;

		public override void _Ready()
		{
			foreach (var arg in OS.GetCmdlineArgs())
			{
				if (arg.StartsWith("--scene="))
					_targetScene = arg.Substring("--scene=".Length).Trim();
				else if (arg.StartsWith("--choices="))
					_choices = arg.Substring("--choices=".Length).Trim();
				else if (arg.StartsWith("--timeout="))
				{
					if (float.TryParse(arg.Substring("--timeout=".Length), out var t) && t > 0)
						TimeoutSeconds = t;
				}
			}

			if (string.IsNullOrEmpty(_targetScene))
			{
				GameLogger.Log("TEST", "SceneSwitcher: --scene= not specified, quitting", LogLevel.Error);
				GetTree().Quit(1);
				return;
			}

			var s = GameData.Instance?.Settings;
			if (s != null)
				s.LauncherShown = true;

			GameLogger.Log("TEST", $"SceneSwitcher: target='{_targetScene}', choices='{_choices}', timeout={TimeoutSeconds}s");

			if (_targetScene.EndsWith(".tscn"))
				LoadGodotScene(_targetScene);
			else
				LoadLogicalScene(_targetScene);
		}

		private void LoadGodotScene(string path)
		{
			var packed = GD.Load<PackedScene>(path);
			if (packed == null)
			{
				GameLogger.Log("TEST", $"SceneSwitcher: failed to load PackedScene '{path}'", LogLevel.Error);
				GetTree().Quit(1);
				return;
			}

			AddChild(packed.Instantiate());
			ScheduleDriverAttach();
		}

		private void LoadLogicalScene(string sceneName)
		{
			var baseScene = GD.Load<PackedScene>("res://scenes/ui/base_scene.tscn");
			if (baseScene == null)
			{
				GameLogger.Log("TEST", "SceneSwitcher: failed to load base_scene.tscn", LogLevel.Error);
				GetTree().Quit(1);
				return;
			}

			AddChild(baseScene.Instantiate());

			GetTree().CreateTimer(0.2f).Timeout += () =>
			{
				var sm = GetNodeOrNull<SceneManager>("/root/SceneManager");
				if (sm == null)
				{
					GameLogger.Log("TEST", "SceneSwitcher: SceneManager autoload not found", LogLevel.Error);
					GetTree().Quit(1);
					return;
				}

				if (!sm.HasScene(sceneName))
				{
					GameLogger.Log("TEST", $"SceneSwitcher: logical scene '{sceneName}' not found", LogLevel.Error);
					GetTree().Quit(1);
					return;
				}

				GameLogger.Log("TEST", $"SceneSwitcher: switching to logical scene '{sceneName}'");
				sm.SwitchScene(sceneName);
			};
			ScheduleDriverAttach();
		}

		private void ScheduleDriverAttach()
		{
			GetTree().CreateTimer(0.5f).Timeout += () =>
			{
				if (_driverAttached || !GodotObject.IsInstanceValid(this))
					return;

				_driverAttached = true;
				var driver = new DialogAutoDriver();
				AddChild(driver);
				GameLogger.Log("TEST", "SceneSwitcher: DialogAutoDriver attached");
			};
		}

		public override void _Process(double delta)
		{
			_elapsed += (float)delta;
			if (_elapsed >= TimeoutSeconds)
			{
				GameLogger.Log("TEST", $"SceneSwitcher: timeout ({TimeoutSeconds}s) reached, quitting");
				GetTree().Quit();
			}
		}
	}
}
#endif
