using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Items;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Inventory;
using Cthangover.Core.Utils;
using Cthangover.Mods.Items;
using Cthangover.Mods.Rpg;
using Cthangover.Mods.Interface.View;
using Godot;

namespace Cthangover.Mods.Trading.Tests
{
    /// <summary>
    /// Plays the gathering and trading loop of rpg_demo like a player: the hero
    /// walks onto things in the forest (key events through
    /// <see cref="Input.ParseInputEvent"/>), eats a mushroom, talks to the
    /// merchant with E and trades in the window by clicking its cells and
    /// buttons (the signals a mouse would raise).
    /// <para/>
    /// One step per timer tick; a step either waits (returns without advancing)
    /// or checks and moves on. A failed check is recorded and the run goes on; an
    /// exception ends it. Result line: <c>[TEST_RESULT] name=trade passed=N
    /// failed=M</c>, exit code 0/1, and a 90 s watchdog.
    /// </summary>
    public partial class TradeTestDriver : Node
    {
        private const float TickSeconds = 0.1f;
        private const float TimeoutSeconds = 90f;
        private const string Village = "rpg_village";
        private const string Forest = "rpg_forest";
        private const string Branches = "rpg_demo/branches";
        private const string Stones = "rpg_demo/stones";
        private const string Mushroom = "rpg_demo/mushroom";
        private const string Elixir = "rpg_demo/elixir";
        private const string Coin = ShopInfo.DefaultCurrency;

        private readonly string shotsDir;
        private readonly List<string> failures = new();
        private int passed;
        private int step;
        private int waitTicks;
        private double elapsed;

        /// <summary>Creates the driver; <paramref name="shotsDir"/> null disables screenshots.</summary>
        public TradeTestDriver(string shotsDir)
        {
            this.shotsDir = shotsDir;
            Name = "TradeTestDriver";
            var timer = new Timer { WaitTime = TickSeconds, Autostart = true };
            AddChild(timer);
            timer.Timeout += Tick;
        }

        private static RpgWorldView View => RpgWorldHost.Current?.View;

        private static IInventory Inventory => GameData.Instance.Runtime.Inventory;

        private void Tick()
        {
            if (step < 0)
                return;
            elapsed += TickSeconds;
            if (elapsed > TimeoutSeconds)
            {
                Fail($"timeout at step {step}");
                Finish();
                return;
            }

            try
            {
                RunStep();
            }
            catch (System.Exception ex)
            {
                Fail($"step {step}: {ex.GetType().Name}: {ex.Message}");
                Finish();
            }
        }

        private void RunStep()
        {
            switch (step)
            {
                case 0:
                    if (Boot.IsReady && SkipPrologue())
                        return;
                    if (Boot.IsReady && View?.Map?.ID == Village)
                        Next();
                    return;
                case 1:
                    if (AdvanceDialog())
                        return;
                    // The prologue gives a purse; the deals below count from an empty one.
                    if (Inventory.CheckCount(Coin) > 0)
                        Inventory.Remove(Coin, Inventory.CheckCount(Coin));
                    Check(Inventory.CheckCount(Coin) == 0, "the purse is emptied for the test");
                    RpgWorldHost.Teleport(new RpgTeleport { Map = Forest, X = 6, Y = 4, Dir = RpgDirection.Down });
                    Next();
                    return;
                case 2:
                    if (!Settled(Forest) || AdvanceDialog())
                        return;
                    Check(View.FindEvent("pick_branches_1")?.Visible == true, "brushwood lies in the forest");
                    Press(Key.S, true);
                    Next();
                    return;
                case 3:
                    if (View.Player.Cell.Y < 5)
                        return;
                    Press(Key.S, false);
                    Next();
                    return;
                case 4:
                    if (!Settled(Forest) || AdvanceDialog() || ++waitTicks < 3)
                        return;
                    Check(Inventory.CheckCount(Branches) == 2, $"stepping on brushwood picks up 2, got {Inventory.CheckCount(Branches)}");
                    Check(View.FindEvent("pick_branches_1")?.Visible == false, "picked brushwood disappears");
                    View.PlacePlayer(11, 2, RpgDirection.Down);
                    Next();
                    return;
                case 5:
                    if (StepDown(3))
                        return;
                    Check(Inventory.CheckCount(Stones) == 2, $"stones picked up, got {Inventory.CheckCount(Stones)}");
                    View.PlacePlayer(10, 2, RpgDirection.Down);
                    Next();
                    return;
                case 6:
                    if (StepDown(3))
                        return;
                    Check(Inventory.CheckCount(Mushroom) == 1, $"a mushroom picked up, got {Inventory.CheckCount(Mushroom)}");
                    Shot("forest");
                    CheckMushroomHeals();
                    RpgWorldHost.Teleport(new RpgTeleport { Map = Village, X = 21, Y = 8, Dir = RpgDirection.Right });
                    Next();
                    return;
                case 7:
                    if (!Settled(Village) || AdvanceDialog())
                        return;
                    Tap(Key.E);
                    Next();
                    return;
                case 8:
                    Check(RpgInput.FindDialogBox()?.IsRunning == true, "E talks to the merchant");
                    Next();
                    return;
                case 9:
                    if (AdvanceDialog())
                        return;
                    Check(Panel()?.IsVisibleInTree() == true, "the merchant's dialog opens the trade window");
                    Check(Cells("MerchantGrid").Any(c => c.Model.Item.ID == Elixir), "the merchant offers the elixir");
                    Check(Cells("PlayerGrid").Any(c => c.Model.Item.ID == Branches), "the bag shows the brushwood");
                    Check(Cells("PlayerGrid").All(c => c.Model.Item.ID != Coin), "money is not a cell of the bag");
                    Press(Key.D, true);
                    Next();
                    return;
                case 10:
                    if (++waitTicks < 4)
                        return;
                    Press(Key.D, false);
                    Check(View.Player.Cell == new Vector2I(21, 8), $"the hero does not walk while trading, at {View.Player.Cell}");
                    Click("MerchantGrid", Elixir);
                    Next();
                    return;
                case 11:
                    Check(DealButton("trading/ui/buy") == null, "no buy button without money");
                    Shot("trade_no_money");
                    Click("PlayerGrid", Branches);
                    Next();
                    return;
                case 12:
                    Press(DealButton("trading/ui/all"));
                    Next();
                    return;
                case 13:
                    Press(DealButton("trading/ui/sell"));
                    Next();
                    return;
                case 14:
                    Check(Inventory.CheckCount(Branches) == 0, "all brushwood sold");
                    Check(Inventory.CheckCount(Coin) == 2, $"2 brushwood x 1 coin, got {Inventory.CheckCount(Coin)}");
                    Click("PlayerGrid", Stones);
                    Next();
                    return;
                case 15:
                    Press(DealButton("trading/ui/sell"));
                    Next();
                    return;
                case 16:
                    Check(Inventory.CheckCount(Stones) == 1, "one stone sold, one kept");
                    Check(Inventory.CheckCount(Coin) == 4, $"plus 2 coins for a stone, got {Inventory.CheckCount(Coin)}");
                    Click("PlayerGrid", Mushroom);
                    Next();
                    return;
                case 17:
                    Check(DealButton("trading/ui/sell") == null, "the merchant does not buy mushrooms");
                    Inventory.Add(Coin, 20);
                    Click("MerchantGrid", Elixir);
                    Next();
                    return;
                case 18:
                    Press(DealButton("trading/ui/buy"));
                    Next();
                    return;
                case 19:
                    Check(Inventory.CheckCount(Elixir) == 1, "an elixir bought");
                    Check(Inventory.CheckCount(Coin) == 14, $"24 - 10 coins, got {Inventory.CheckCount(Coin)}");
                    Shot("trade");
                    Press(Key.Escape, true);
                    Press(Key.Escape, false);
                    Next();
                    return;
                case 20:
                    Check(Panel()?.IsVisibleInTree() != true, "Esc closes the trade window");
                    Finish();
                    return;
            }
        }

        /// <summary>The mushroom's action heals the chosen party member by 5 and refuses at full health.</summary>
        private void CheckMushroomHeals()
        {
            var hero = GameData.Instance.Runtime.CharacterData.Characters["rpg_hero"];
            var item = ItemFactory.Instance.Get(Mushroom);
            var heal = item?.ItemAction as ITargetedItemAction;
            Check(heal != null, "the mushroom has a targeted heal action");
            if (heal == null)
                return;
            var max = hero.Attributes.Health.BaseValue;
            hero.Attributes.Health.Value = max - 8;
            Check(heal.UseOn(item, hero) && hero.Attributes.Health.Value == max - 3, $"eating heals 5, health {hero.Attributes.Health.Value}/{max}");
            hero.Attributes.Health.Value = max;
            Check(!heal.UseOn(item, hero), "no eating at full health");
        }

        /// <summary>
        /// Holds S until the hero leaves row 2 (the world reads the held key each
        /// frame, a tap within one tick would be missed), then waits for the touch
        /// event's dialog to end; true while busy.
        /// </summary>
        private bool StepDown(int settleTicks)
        {
            waitTicks++;
            if (waitTicks == 1)
            {
                Press(Key.S, true);
                return true;
            }
            if (View.Player.Cell.Y < 3)
                return true;
            Press(Key.S, false);
            return !Settled(Forest) || View.Player.IsMoving || AdvanceDialog() || waitTicks < settleTicks + 1;
        }

        private static bool Settled(string map) => View?.Map?.ID == map && !GodotSceneService.IsTransitioning;

        private static TradePanel Panel()
        {
            return UiHost.Instance != null && UiHost.Instance.TryGetInstance(TradePanel.EntryId, out var control)
                ? control as TradePanel
                : null;
        }

        private static List<InventoryItemBehaviour> Cells(string grid)
        {
            var parent = Panel()?.FindChild(grid, true, false);
            return parent == null
                ? new List<InventoryItemBehaviour>()
                : parent.GetChildren().OfType<InventoryItemBehaviour>().Where(c => !c.IsQueuedForDeletion()).ToList();
        }

        private void Click(string grid, string itemId)
        {
            var cell = Cells(grid).FirstOrDefault(c => c.Model?.Item?.ID == itemId);
            Check(cell != null, $"{grid} has a cell of {itemId}");
            cell?.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { Pressed = true, ButtonIndex = MouseButton.Left });
        }

        private static Button DealButton(string key)
        {
            var card = Panel()?.FindChild("DealCard", true, false);
            var text = TranslationServer.Translate(key).ToString();
            return card?.FindChildren("*", "", true, false).OfType<Button>()
                .FirstOrDefault(b => b.Text == text && !b.IsQueuedForDeletion() && !b.Disabled);
        }

        private void Press(Button button)
        {
            Check(button != null, "the deal button is there");
            button?.EmitSignal(BaseButton.SignalName.Pressed);
        }

        /// <summary>Advances a running dialog with the action key; true while it is still running.</summary>
        /// <summary>
        /// The game opens with a novel-style prologue (no map yet): its lines are clicked
        /// through and every choice takes the first answer. True while it runs.
        /// </summary>
        private static bool SkipPrologue()
        {
            // Asking for the dialog box before the first scene is up caches the wrong node.
            var scene = ((SceneTree)Engine.GetMainLoop()).Root.GetNodeOrNull<SceneManager>("SceneManager")?.CurrentSceneName;
            if (string.IsNullOrEmpty(scene) || RpgWorldHost.Current?.View?.Map != null)
                return false;
            var box = RpgInput.FindDialogBox();
            if (box == null || !box.IsRunning)
                return false;
            if (box.IsWaitingAnswer)
                box.TrySelectVariant(0);
            else
                box.NextAction();
            return true;
        }

        private static bool AdvanceDialog()
        {
            var box = RpgInput.FindDialogBox();
            if (box == null || !box.IsRunning)
                return false;
            Tap(Key.E);
            return true;
        }

        private static void Press(Key key, bool pressed)
        {
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = pressed });
        }

        private static void Tap(Key key)
        {
            Press(key, true);
            Press(key, false);
        }

        private void Next()
        {
            step++;
            waitTicks = 0;
        }

        private void Check(bool condition, string what)
        {
            if (condition)
            {
                passed++;
                GameLogger.Log("TEST", $"[PASS] {what}");
            }
            else
            {
                Fail(what);
            }
        }

        private void Fail(string what)
        {
            failures.Add(what);
            GameLogger.Log("TEST", $"[FAIL] {what}", LogLevel.Error);
        }

        private void Shot(string name)
        {
            if (string.IsNullOrEmpty(shotsDir) || DisplayServer.GetName() == "headless")
                return;
            DirAccess.MakeDirRecursiveAbsolute(shotsDir);
            ViewportCapture.SavePng(GetViewport(), shotsDir.TrimEnd('/') + "/trade_" + name + ".png");
        }

        private void Finish()
        {
            step = -1;
            GameLogger.Log("TEST", $"[TEST_RESULT] name=trade passed={passed} failed={failures.Count}");
            GetTree().Quit(failures.Count == 0 ? 0 : 1);
        }
    }
}
