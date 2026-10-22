#if TOOLS
using System.Collections.Generic;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Menu;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Autotest
{
	/// <summary>
	/// Automated smoke runs of the main menu. Three modes share the same setup:
	/// <list type="bullet">
	/// <item><c>--test=menu</c> — waits for the menu to settle, clicks "New Game",
	/// then attaches a <see cref="DialogAutoDriver"/> to the tree root so it
	/// survives the scene change and auto-advances the opening scenario until it
	/// ends (exit code 0).</item>
	/// <item><c>--test=mod_manager</c> — clicks the single hub button from the
	/// mod <c>main_menu</c> registry, then verifies the hub opened on the mods
	/// tab with the embedded manager: its grid exists, the tiles have one size and
	/// every status label shows its whole text; a tile opens the mod's card, the
	/// hub's back button returns from it to the grid (not to the menu) and only a
	/// second press leaves the hub (exit code 0). <c>--shots=&lt;dir&gt;</c> saves the
	/// grid and the card (windowed runs only).</item>
	/// <item><c>--test=mod_collections</c> — opens the same hub, switches to the
	/// collections tab, verifies the games gallery, opens the scheme editor from
	/// the first game card and checks the game picker is populated and the
	/// canvas contains at least one group card and one mod chip (exit code 0).</item>
	/// </list>
	/// A watchdog quits with exit code 1 if the flow does not finish within
	/// <c>--timeout=</c> seconds (default 120), so a run can never hang. The hub
	/// hosts the manager/gallery/editor screens, so every check below addresses
	/// them through type-name searches inside <c>ModHubScreen</c> — the test
	/// assembly cannot reference mod types directly.
	/// </summary>
	public partial class MainMenuTestSetup : Node
	{
		private const float ClickDelaySeconds = 0.5f;
		private const float DriverAttachDelaySeconds = 1.0f;
		private const float ModManagerSettleSeconds = 1.0f;
		private const float DefaultTimeoutSeconds = 120f;

		private Node _menu;
		private float _timer = ClickDelaySeconds;
		private float _timeoutSeconds = DefaultTimeoutSeconds;
		private string _testName;
		private string _shotsDir;
		private Node _screen;
		private bool _enabled;
		private bool _started;

		public override void _Ready()
		{
			foreach (var arg in OS.GetCmdlineArgs())
			{
				if (arg.StartsWith("--test="))
				{
					var testName = arg.Substring("--test=".Length).ToLower();
					if (testName == "menu" || testName == "mod_manager" || testName == "mod_collections")
					{
						_testName = testName;
						_enabled = true;
						GameLogger.Log("TEST", $"MainMenuTestSetup: auto-test enabled via --test={testName}");
					}
				}
				else if (arg.StartsWith("--shots="))
				{
					_shotsDir = arg.Substring("--shots=".Length);
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

			if (_menu == null)
				BuildMenu();

			_timer -= (float)delta;
			if (_timer > 0)
				return;

			SetProcess(false);
			_started = true;

			if (_testName == "mod_manager")
				RunModManagerTest();
			else if (_testName == "mod_collections")
				RunModCollectionsTest();
			else
				RunNewGameTest();
		}

		/// <summary>
		/// Builds the main menu from the menu_base mod at runtime. The old native
		/// res://scenes/menu/main_menu.tscn no longer exists, so the test scene
		/// cannot instance it statically; the mod-resolved scene is attached as a
		/// child of this setup node and all lookups go through <c>_menu</c>.
		/// </summary>
		private void BuildMenu()
		{
			_menu = TscnScenes.LoadAndBuild("scenes/menu/main_menu.tscn");
			if (_menu == null)
			{
				GameLogger.Log("TEST", "MainMenuTestSetup: failed to build the main menu scene from mods", LogLevel.Error);
				Fail("menu");
				return;
			}

			_menu.Name = "MainMenuScene";
			AddChild(_menu);
			GameLogger.Log("TEST", "MainMenuTestSetup: main menu scene built from mods");
		}

		private void RunNewGameTest()
		{
			var newGameBtn = _menu?.GetNodeOrNull<Button>("MenuContainer/GameButtons/new_game");
			if (newGameBtn == null)
			{
				GameLogger.Log("TEST", "MainMenuTestSetup: new_game button not found, quitting", LogLevel.Error);
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

		/// <summary>
		/// Clicks the single hub button (the only <c>main_menu</c> entry since
		/// the mods and collections screens merged into one hub).
		/// </summary>
		private Button FindHubButton()
		{
			var container = _menu?.GetNodeOrNull<Control>("MenuContainer/ModMenuButtons");
			return container != null && container.GetChildCount() > 0
				? container.GetChild(0) as Button
				: null;
		}

		private Node FindHub()
			=> _menu?.GetNodeOrNull<Node>("ModHubScreen");

		private void RunModManagerTest()
		{
			var button = FindHubButton();
			if (button == null)
			{
				GameLogger.Log("TEST", "MainMenuTestSetup: no mod main-menu buttons found, quitting", LogLevel.Error);
				Fail("mod_manager");
				return;
			}

			GameLogger.Log("TEST", "MainMenuTestSetup: clicking mods & collections hub button");
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

			// The hub opens on the mods tab with the manager embedded inside.
			var hub = FindHub();
			var screen = hub != null ? FindFirstDescendantByTypeName(hub, "ModManagerScreen") : null;
			if (screen == null)
			{
				GameLogger.Log("TEST", "ModManagerScreen not found inside the hub after button click", LogLevel.Error);
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

			// The status ("Installed", "Modified locally") must not be squeezed out by its
			// neighbours in the row (it once shared the row with "in N collections").
			foreach (var card in cards)
			{
				var status = card.GetNodeOrNull<Label>("VBox/Body/Box/MetaRow/Status");
				if (status == null || string.IsNullOrEmpty(status.Text))
					continue;
				var width = status.GetThemeFont("font").GetStringSize(status.Text, HorizontalAlignment.Left, -1, status.GetThemeFontSize("font_size")).X;
				if (width <= status.Size.X + 0.5f)
					continue;
				GameLogger.Log("TEST",
					$"MainMenuTestSetup: status '{status.Text}' of {card.Name} is cut ({width:0} px text in a {status.Size.X:0} px label)",
					LogLevel.Error);
				Fail("mod_manager");
				return;
			}
			SaveShot("mod_grid");

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
			SaveShot("mod_detail");

			var back = FindButtonByText(FindHub(), TranslationServer.Translate("mod_manager/hub_back_to_mods"));
			if (back == null)
			{
				GameLogger.Log("TEST", "MainMenuTestSetup: with a mod open the hub's back button does not lead to the mods", LogLevel.Error);
				Fail("mod_manager");
				return;
			}

			_screen = screen;
			back.EmitSignal("pressed");
			GetTree().CreateTimer(ModManagerSettleSeconds).Timeout += VerifyBackToMods;
		}

		/// <summary>Back from a mod's card shows the grid again inside the still open hub.</summary>
		private void VerifyBackToMods()
		{
			if (!GodotObject.IsInstanceValid(this))
				return;

			var hub = FindHub();
			var detail = GodotObject.IsInstanceValid(_screen) ? FindFirstDescendantByTypeName(_screen, "ModDetailView") as Control : null;
			var grid = GodotObject.IsInstanceValid(_screen) ? FindFirstDescendantByTypeName(_screen, "ModGridView") as Control : null;
			if (hub == null || hub.IsQueuedForDeletion() || detail == null || detail.Visible || grid == null || !grid.Visible)
			{
				GameLogger.Log("TEST",
					$"MainMenuTestSetup: back from a mod did not return to the grid (hub={hub != null}, detail visible={detail?.Visible}, grid visible={grid?.Visible})",
					LogLevel.Error);
				Fail("mod_manager");
				return;
			}

			var back = FindButtonByText(hub, TranslationServer.Translate("mod_manager/hub_back_to_menu"));
			if (back == null)
			{
				GameLogger.Log("TEST", "MainMenuTestSetup: on the grid the back button does not lead to the menu", LogLevel.Error);
				Fail("mod_manager");
				return;
			}

			GameLogger.Log("TEST", "MainMenuTestSetup: back from the mod card returned to the mods grid");
			back.EmitSignal("pressed");
			GetTree().CreateTimer(ModManagerSettleSeconds).Timeout += VerifyBackToMenu;
		}

		/// <summary>The second back leaves the hub to the menu.</summary>
		private void VerifyBackToMenu()
		{
			if (!GodotObject.IsInstanceValid(this))
				return;

			var hub = FindHub();
			if (hub != null && !hub.IsQueuedForDeletion())
			{
				GameLogger.Log("TEST", "MainMenuTestSetup: back on the mods grid did not leave the hub", LogLevel.Error);
				Fail("mod_manager");
				return;
			}

			GameLogger.Log("TEST", "MainMenuTestSetup: the second back returned to the menu");
			GameLogger.Log("TEST", "[TEST_RESULT] name=mod_manager passed=1 failed=0");
			GetTree().Quit(0);
		}

		/// <summary>With <c>--shots=&lt;dir&gt;</c> saves the window as <c>&lt;name&gt;.png</c> (windowed runs only).</summary>
		private void SaveShot(string name)
		{
			if (string.IsNullOrEmpty(_shotsDir) || DisplayServer.GetName() == "headless")
				return;
			DirAccess.MakeDirRecursiveAbsolute(_shotsDir);
			GetViewport().GetTexture().GetImage()?.SavePng(_shotsDir.TrimEnd('/', '\\') + "/" + name + ".png");
		}

		private void RunModCollectionsTest()
		{
			var button = FindHubButton();
			if (button == null)
			{
				GameLogger.Log("TEST", "MainMenuTestSetup: hub button not found, quitting", LogLevel.Error);
				Fail("mod_collections");
				return;
			}

			GameLogger.Log("TEST", "MainMenuTestSetup: clicking mods & collections hub button");
			button.EmitSignal("pressed");

			GetTree().CreateTimer(ModManagerSettleSeconds).Timeout += OpenCollectionsTab;
			GetTree().CreateTimer(_timeoutSeconds).Timeout += () =>
			{
				GameLogger.Log("TEST", $"MainMenuTestSetup: timeout ({_timeoutSeconds}s) reached in mod_collections test, quitting", LogLevel.Error);
				Fail("mod_collections");
			};
		}

		/// <summary>Switches the hub to the collections tab via its segment button.</summary>
		private void OpenCollectionsTab()
		{
			if (!GodotObject.IsInstanceValid(this))
				return;

			var hub = FindHub();
			var tab = hub != null
				? FindButtonByText(hub, TranslationServer.Translate("mod_manager/hub_tab_collections"))
				: null;

			if (tab == null)
			{
				GameLogger.Log("TEST", "MainMenuTestSetup: collections tab button not found in the hub", LogLevel.Error);
				Fail("mod_collections");
				return;
			}

			tab.EmitSignal("pressed");
			GetTree().CreateTimer(ModManagerSettleSeconds).Timeout += VerifyCollectionsScreen;
		}

		private void VerifyCollectionsScreen()
		{
			if (!GodotObject.IsInstanceValid(this))
				return;

			var hub = FindHub();
			var gallery = hub != null ? FindFirstDescendantByTypeName(hub, "CollectionsGallery") : null;
			if (gallery == null)
			{
				GameLogger.Log("TEST", "MainMenuTestSetup: CollectionsGallery not found on the collections tab", LogLevel.Error);
				Fail("mod_collections");
				return;
			}

			// Open the scheme editor from the first game card ("Edit scheme").
			var edit = FindButtonByText(gallery, TranslationServer.Translate("mod_manager/gallery_edit_scheme"));
			if (edit == null)
			{
				GameLogger.Log("TEST", "MainMenuTestSetup: no game cards in the gallery (no edit-scheme button)", LogLevel.Error);
				Fail("mod_collections");
				return;
			}

			GameLogger.Log("TEST", "MainMenuTestSetup: gallery OK, opening the scheme editor");
			edit.EmitSignal("pressed");

			GetTree().CreateTimer(ModManagerSettleSeconds).Timeout += VerifySchemeEditor;
		}

		private void VerifySchemeEditor()
		{
			if (!GodotObject.IsInstanceValid(this))
				return;

			var hub = FindHub();
			var screen = hub != null ? FindFirstDescendantByTypeName(hub, "ModCollectionsScreen") : null;
			if (screen == null)
			{
				GameLogger.Log("TEST", "ModCollectionsScreen not found after opening the editor", LogLevel.Error);
				Fail("mod_collections");
				return;
			}

			var canvas = FindFirstDescendantByTypeName(screen, "CollectionSchemeCanvas");
			var cards = CountDescendantsByTypeName(screen, "CollectionGroupCard");
			var chips = CountDescendantsByTypeName(screen, "CollectionModChip");

			// The game picker moved from the header into the breadcrumbs, so it
			// is located by type — it is the only OptionButton on this screen.
			var gameSelect = FindFirstDescendantByTypeName(screen, "OptionButton") as OptionButton;
			var games = gameSelect?.ItemCount ?? 0;

			if (canvas == null || cards == 0 || chips == 0 || games == 0)
			{
				GameLogger.Log("TEST",
					$"MainMenuTestSetup: collections scheme built incompletely (canvas={canvas != null}, cards={cards}, chips={chips}, games={games})",
					LogLevel.Error);
				Fail("mod_collections");
				return;
			}

			GameLogger.Log("TEST",
				$"MainMenuTestSetup: collections OK, {games} game(s), {cards} group(s), {chips} mod(s)");
			GameLogger.Log("TEST", "[TEST_RESULT] name=mod_collections passed=1 failed=0");
			GetTree().Quit(0);
		}

		/// <summary>Finds the first descendant button with the given visible text.</summary>
		private static Button FindButtonByText(Node root, string text)
		{
			foreach (var child in root.GetChildren())
			{
				if (child is Button button && button.Text == text)
					return button;

				var found = FindButtonByText(child, text);
				if (found != null)
					return found;
			}
			return null;
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
