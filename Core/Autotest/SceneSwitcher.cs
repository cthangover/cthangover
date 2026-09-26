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
		private string _ui;
		private string _load;
		private bool _reset;
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
				else if (arg.StartsWith("--ui="))
					_ui = arg.Substring("--ui=".Length).Trim();
				else if (arg.StartsWith("--load="))
					_load = arg.Substring("--load=".Length).Trim();
				else if (arg == "--reset")
					_reset = true;
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

			if (_reset)
			{
				GameData.Instance?.StartNewGame();
				GameLogger.Log("TEST", "SceneSwitcher: StartNewGame() applied (simulates New Game)");
			}

			GameLogger.Log("TEST", $"SceneSwitcher: target='{_targetScene}', choices='{_choices}', timeout={TimeoutSeconds}s");

			if (_targetScene.EndsWith(".tscn"))
				LoadGodotScene(_targetScene);
			else
				LoadLogicalScene(_targetScene);

			ScheduleLoadCheck();
		}

		/// <summary>
		/// Test hook for arbitrary mod scenes: builds every path from <c>--load=</c>
		/// through <see cref="TscnScenes.LoadAndBuild"/> and logs the resulting root
		/// type. Unlike <c>--ui=</c> it needs no registry entry, so it can validate
		/// scenes that are only reachable through other UI (e.g. mods_menu).
		/// </summary>
		private void ScheduleLoadCheck()
		{
			if (string.IsNullOrEmpty(_load))
				return;

			GetTree().CreateTimer(0.8f).Timeout += () =>
			{
				foreach (var raw in _load.Split(','))
				{
					var path = raw.Trim();
					if (string.IsNullOrEmpty(path))
						continue;

					var node = TscnScenes.LoadAndBuild(path);
					if (node == null)
					{
						GameLogger.Log("TEST", $"SceneSwitcher: load '{path}' FAILED", LogLevel.Error);
						continue;
					}

					GameLogger.Log("TEST", $"SceneSwitcher: load '{path}' OK -> root='{node.Name}' ({node.GetType().Name})");
					node.QueueFree();
				}
			};
		}

		private void LoadGodotScene(string path)
		{
			var node = InstantiateScene(path);
			if (node == null)
			{
				GameLogger.Log("TEST", $"SceneSwitcher: failed to instantiate scene '{path}'", LogLevel.Error);
				GetTree().Quit(1);
				return;
			}

			AddChild(node);
			ScheduleDriverAttach();
			ScheduleUiToggle();
		}

		private void LoadLogicalScene(string sceneName)
		{
			var baseScene = InstantiateScene(GodotSceneService.GetScenePath(GodotSceneType.BaseScene));
			if (baseScene == null)
			{
				GameLogger.Log("TEST", "SceneSwitcher: failed to instantiate base_scene", LogLevel.Error);
				GetTree().Quit(1);
				return;
			}

			AddChild(baseScene);

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
			ScheduleUiToggle();
		}

		/// <summary>
		/// Instantiates a scene target for testing: native <c>res://</c> scenes go
		/// through Godot's loader, while mod-relative paths (the shell and battle
		/// scenes moved into mods) are built by the runtime tscn pipeline. Keeps
		/// <c>--scene=</c> usable for both kinds without hardcoding which is which.
		/// </summary>
		private static Node InstantiateScene(string path)
		{
			if (string.IsNullOrEmpty(path))
				return null;

			if (path.StartsWith("res://"))
				return GD.Load<PackedScene>(path)?.Instantiate();

			return TscnScenes.LoadAndBuild(path);
		}

		/// <summary>
		/// Test hook for the mod GUI pipeline: after the shell is up, toggles every
		/// id from <c>--ui=</c> through <see cref="UI.UiRegistry.Toggle"/>. This
		/// exercises registry lookup, TscnScenes construction and core-script
		/// resolution for mod-provided panels — failures surface as ERROR logs.
		/// </summary>
		private void ScheduleUiToggle()
		{
			if (string.IsNullOrEmpty(_ui))
				return;

			GetTree().CreateTimer(1.0f).Timeout += () =>
			{
				foreach (var raw in _ui.Split(','))
				{
					var id = raw.Trim();
					if (string.IsNullOrEmpty(id))
						continue;

					var entry = UI.UiRegistry.Get(id);
					if (entry == null)
					{
						GameLogger.Log("TEST", $"SceneSwitcher: ui entry '{id}' not found", LogLevel.Error);
						continue;
					}

					GameLogger.Log("TEST", $"SceneSwitcher: toggling ui '{id}' ({entry.ScenePath})");
					UI.UiRegistry.Toggle(id);
				}

				GetTree().CreateTimer(0.5f).Timeout += DumpUiState;
			};
		}

		/// <summary>
		/// Diagnostic dump used by the UI smoke runs: logs party/action-pool data and
		/// the opened panels' node trees (type, visibility, size) so layout and
		/// data-binding problems are visible in headless logs.
		/// </summary>
		private void DumpUiState()
		{
			var shotPath = "user://ui_smoke.png";
			GetViewport()?.GetTexture()?.GetImage()?.SavePng(shotPath);
			GameLogger.Log("TEST", $"SceneSwitcher: screenshot -> {ProjectSettings.GlobalizePath(shotPath)}");

			var runtime = GameData.Instance?.Runtime;
			if (runtime != null)
			{
				GameLogger.Log("TEST", $"SceneSwitcher: BattleSet=[{string.Join(",", runtime.CharacterData.BattleSet)}] "
					+ $"chars=[{string.Join(",", runtime.CharacterData.Characters.Keys)}] "
					+ $"actionPool=[{string.Join(",", runtime.ActionPool.ActionIds)}]");
			}

			foreach (var raw in _ui.Split(','))
			{
				var id = raw.Trim();
				if (string.IsNullOrEmpty(id))
					continue;

				var node = SceneContextNode.FindNode<Control>(id);
				if (node == null)
				{
					GameLogger.Log("TEST", $"SceneSwitcher: dump '{id}': instance not found", LogLevel.Error);
					continue;
				}

				GameLogger.Log("TEST", $"SceneSwitcher: dump '{id}' -> {node.GetType().Name}");
				DumpNode(node, 1);
			}
		}

		private static void DumpNode(Node node, int depth)
		{
			if (node == null || depth > 14)
				return;

			var indent = new string(' ', depth * 2);
			var text = node switch
			{
				Label label => label.Text,
				RichTextLabel rich => rich.Text,
				_ => null
			};

			if (node is Control control)
				GameLogger.Log("TEST", $"  {indent}{node.Name} [{node.GetType().Name}] vis={control.Visible} size={control.Size} text='{text}'");
			else
				GameLogger.Log("TEST", $"  {indent}{node.Name} [{node.GetType().Name}]");

			foreach (var child in node.GetChildren())
				DumpNode(child, depth + 1);
		}

		private void ScheduleDriverAttach()
		{
			GetTree().CreateTimer(0.5f).Timeout += () =>
			{
				if (_driverAttached || !GodotObject.IsInstanceValid(this))
					return;

				_driverAttached = true;
				var driver = new DialogAutoDriver
				{
					// UI/load smoke runs need the process to survive the end of a
					// dialog: the toggle/dump timers fire after the dialog has
					// already finished, so quitting on dialog end would hide the
					// very state the run inspects.
					AutoQuit = string.IsNullOrEmpty(_ui) && string.IsNullOrEmpty(_load)
				};
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
