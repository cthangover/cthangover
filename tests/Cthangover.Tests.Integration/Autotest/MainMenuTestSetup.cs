#if TOOLS
using System.Collections.Generic;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
	/// <summary>
	/// Automated smoke runs of the main menu. Two modes share the same setup:
	/// <list type="bullet">
	/// <item><c>--test=menu</c> — waits for the menu to settle, clicks "New Game",
	/// then attaches a <see cref="DialogAutoDriver"/> to the tree root so it
	/// survives the scene change and auto-advances the opening scenario until it
	/// ends (exit code 0).</item>
	/// <item><c>--test=mod_manager</c> — clicks the first button provided by the
	/// mod <c>main_menu</c> registry (the mod_manager button), then verifies the
	/// mod scene was built, its grid exists and at least one mod card was created
	/// (exit code 0).</item>
	/// </list>
	/// A watchdog quits with exit code 1 if the flow does not finish within
	/// <c>--timeout=</c> seconds (default 120), so a run can never hang.
	/// </summary>
	public partial class MainMenuTestSetup : Node
	{
		private const float ClickDelaySeconds = 0.5f;
		private const float DriverAttachDelaySeconds = 1.0f;
		private const float ModManagerSettleSeconds = 1.0f;
		private const float DefaultTimeoutSeconds = 120f;

		private float _timer = ClickDelaySeconds;
		private float _timeoutSeconds = DefaultTimeoutSeconds;
		private string _testName;
		private bool _enabled;
		private bool _started;

		public override void _Ready()
		{
			foreach (var arg in OS.GetCmdlineArgs())
			{
				if (arg.StartsWith("--test="))
				{
					var testName = arg.Substring("--test=".Length).ToLower();
					if (testName == "menu" || testName == "mod_manager")
					{
						_testName = testName;
						_enabled = true;
						GameLogger.Log("TEST", $"MainMenuTestSetup: auto-test enabled via --test={testName}");
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
			_started = true;

			if (_testName == "mod_manager")
				RunModManagerTest();
			else
				RunNewGameTest();
		}

		private void RunNewGameTest()
		{
			var newGameBtn = GetParent().GetNodeOrNull<Button>("MenuContainer/NewGameBtn");
			if (newGameBtn == null)
			{
				GameLogger.Log("TEST", "MainMenuTestSetup: NewGameBtn not found, quitting", LogLevel.Error);
				Fail("menu");
				return;
			}

			var tree = GetTree();

			tree.CreateTimer(DriverAttachDelaySeconds).Timeout += () =>
			{
				var driver = new DialogAutoDriver { ResultName = "menu" };
				tree.Root.AddChild(driver);
			};
			tree.CreateTimer(_timeoutSeconds).Timeout += () =>
			{
				GameLogger.Log("TEST", $"MainMenuTestSetup: timeout ({_timeoutSeconds}s) reached without dialog end, quitting", LogLevel.Error);
				Fail("menu");
			};

			GameLogger.Log("TEST", "MainMenuTestSetup: clicking 'New Game' button");
			newGameBtn.EmitSignal("pressed");
		}

		private void RunModManagerTest()
		{
			var container = GetParent().GetNodeOrNull<Control>("MenuContainer/ModMenuButtons");
			var button = container != null && container.GetChildCount() > 0
				? container.GetChild(0) as Button
				: null;

			if (button == null)
			{
				GameLogger.Log("TEST", "MainMenuTestSetup: no mod main-menu buttons found, quitting", LogLevel.Error);
				Fail("mod_manager");
				return;
			}

			GameLogger.Log("TEST", "MainMenuTestSetup: clicking first mod main-menu button");
			button.EmitSignal("pressed");

			GetTree().CreateTimer(ModManagerSettleSeconds).Timeout += VerifyModManagerScreen;
			GetTree().CreateTimer(_timeoutSeconds).Timeout += () =>
			{
				GameLogger.Log("TEST", $"MainMenuTestSetup: timeout ({_timeoutSeconds}s) reached in mod_manager test, quitting", LogLevel.Error);
				Fail("mod_manager");
			};
		}

		private void VerifyModManagerScreen()
		{
			if (!GodotObject.IsInstanceValid(this))
				return;

			var screen = GetParent().GetNodeOrNull<Node>("ModManagerScreen");
			if (screen == null)
			{
				GameLogger.Log("TEST", "MainMenuTestSetup: ModManagerScreen not found after button click", LogLevel.Error);
				Fail("mod_manager");
				return;
			}

			var grid = FindFirstDescendantByTypeName(screen, "ModGridView");
			var cards = CountDescendantsByTypeName(screen, "ModCard");

			if (grid == null || cards == 0)
			{
				GameLogger.Log("TEST",
					$"MainMenuTestSetup: mod manager built incompletely (grid={grid != null}, cards={cards})",
					LogLevel.Error);
				Fail("mod_manager");
				return;
			}

			GameLogger.Log("TEST", $"MainMenuTestSetup: mod manager OK, {cards} card(s)");

			GetTree().CreateTimer(ModManagerSettleSeconds).Timeout += () => VerifyCardSizes(screen);
		}

		/// <summary>
		/// All tiles must render at exactly the same size regardless of whether a
		/// mod has an author, catalog entry, versions or an action button, so the
		/// grid stays visually even.
		/// </summary>
		private void VerifyCardSizes(Node screen)
		{
			if (!GodotObject.IsInstanceValid(this) || !GodotObject.IsInstanceValid(screen))
				return;

			var cards = new List<Control>();
			CollectDescendantsByTypeName(screen, "ModCard", cards);
			if (cards.Count == 0)
			{
				GameLogger.Log("TEST", "MainMenuTestSetup: no mod cards after layout", LogLevel.Error);
				Fail("mod_manager");
				return;
			}

			var expected = cards[0].Size;
			foreach (var card in cards)
			{
				if (Mathf.IsEqualApprox(card.Size.X, expected.X) && Mathf.IsEqualApprox(card.Size.Y, expected.Y))
					continue;

				GameLogger.Log("TEST",
					$"MainMenuTestSetup: tile sizes differ ({card.Name}: {card.Size.X}x{card.Size.Y} vs {expected.X}x{expected.Y})",
					LogLevel.Error);
				Fail("mod_manager");
				return;
			}

			GameLogger.Log("TEST", $"MainMenuTestSetup: all {cards.Count} tiles are {expected.X}x{expected.Y}");

			// The cover art must be shifted 1px up and 1px wider than the card body
			// (negative content margins of the panel style), per the design spec.
			var panelStyle = cards[0].GetThemeStylebox("panel") as StyleBoxFlat;
			if (panelStyle == null
				|| !Mathf.IsEqualApprox(panelStyle.ContentMarginTop, -1f)
				|| !Mathf.IsEqualApprox(panelStyle.ContentMarginLeft, -1f))
			{
				GameLogger.Log("TEST",
					$"MainMenuTestSetup: cover offset style missing (top={panelStyle?.ContentMarginTop}, left={panelStyle?.ContentMarginLeft})",
					LogLevel.Error);
				Fail("mod_manager");
				return;
			}

			var click = new InputEventMouseButton
			{
				ButtonIndex = MouseButton.Left,
				Pressed = true,
			};
			cards[0].EmitSignal("gui_input", click);

			GetTree().CreateTimer(ModManagerSettleSeconds).Timeout += () => VerifyModDetail(screen);
		}

		private void VerifyModDetail(Node screen)
		{
			if (!GodotObject.IsInstanceValid(this) || !GodotObject.IsInstanceValid(screen))
				return;

			var detail = FindFirstDescendantByTypeName(screen, "ModDetailView") as Control;
			if (detail == null || !detail.Visible)
			{
				GameLogger.Log("TEST",
					$"MainMenuTestSetup: detail page did not open (found={detail != null}, visible={detail?.Visible})",
					LogLevel.Error);
				Fail("mod_manager");
				return;
			}

			GameLogger.Log("TEST", "MainMenuTestSetup: mod detail page opened");
			GameLogger.Log("TEST", "[TEST_RESULT] name=mod_manager passed=1 failed=0");
			GetTree().Quit(0);
		}

		/// <summary>
		/// Counts descendants whose runtime C# type has the given name. The test
		/// assembly cannot reference mod types, but it can inspect them by
		/// reflection, which keeps the check independent of node naming.
		/// </summary>
		private static int CountDescendantsByTypeName(Node root, string typeName)
		{
			var count = 0;
			foreach (var child in root.GetChildren())
			{
				if (child.GetType().Name == typeName)
					count++;
				count += CountDescendantsByTypeName(child, typeName);
			}
			return count;
		}

		/// <summary>Collects descendants whose runtime C# type has the given name.</summary>
		private static void CollectDescendantsByTypeName(Node root, string typeName, List<Control> result)
		{
			foreach (var child in root.GetChildren())
			{
				if (child.GetType().Name == typeName && child is Control control)
					result.Add(control);
				CollectDescendantsByTypeName(child, typeName, result);
			}
		}

		/// <summary>Finds the first descendant whose runtime C# type has the given name.</summary>
		private static Node FindFirstDescendantByTypeName(Node root, string typeName)
		{
			foreach (var child in root.GetChildren())
			{
				if (child.GetType().Name == typeName)
					return child;

				var found = FindFirstDescendantByTypeName(child, typeName);
				if (found != null)
					return found;
			}
			return null;
		}

		private void Fail(string testName)
		{
			GameLogger.Log("TEST", $"[TEST_RESULT] name={testName} passed=0 failed=1");
			if (GodotObject.IsInstanceValid(this))
				GetTree().Quit(1);
		}
	}
}
#endif
