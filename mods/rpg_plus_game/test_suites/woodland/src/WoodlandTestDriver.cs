namespace Cthangover.Mods.RpgPlus.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Battle;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Games;
    using Cthangover.Core.Items;
    using Cthangover.Core.Quests;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;
    using Cthangover.Core.UI;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Crafting;
    using Cthangover.Mods.Dialog;
    using Cthangover.Mods.Equipment;
    using Cthangover.Mods.Interface.View;
    using Cthangover.Mods.Rpg;
    using Cthangover.Mods.Stats;
    using Cthangover.FFBattle.UI;
    using Godot;

    /// <summary>
    /// Drives NPC event dialogs and the crafting UI, then verifies quest state and rewards
    /// across a real save/load. Battles click the FF attack menus and enemy widgets;
    /// enemies have one HP so lethal-hit reward timing is deterministic.
    /// Gathering is covered by trade/rpg_walk; only mushrooms and branches are fixtures here.
    /// </summary>
    public partial class WoodlandTestDriver : Node
    {
        private readonly Queue<int> choices = new();
        private readonly bool pact;
        private readonly string shots;
        private int step;
        private int ticks;
        private int passed;
        private int failed;
        private int resinBefore;
        private int paidCoins;
        private int savedResin;
        private int villageShotPhase;
        private long shotTime;
        private double elapsed;
        private BattleSceneContext testedBattle;
        private ulong nextAttack;
        private string victoryShot;
        private bool recoveringAmber;
        private const string SaveSlot = "slot_9917";
        private string TestName => pact ? "woodland_pact" : "woodland";
        private static RpgWorldView View => RpgWorldHost.Current?.View;
        private static DialogBox Box => RpgInput.FindDialogBox();
        private static IInventory Bag => GameData.Instance.Runtime.Inventory;
        private static string Flag(string key) => GameData.Instance.Runtime.Flags.TryGetValue(key, out var value) ? value : null;
        private static QuestBase Quest(string id) => (QuestBase)QuestFactory.Instance.Get("woodland_" + id);

        /// <summary>Two deterministic choice paths share the same checks and isolated test slot.</summary>
        public WoodlandTestDriver(bool pact, string shots)
        {
            this.pact = pact;
            this.shots = shots;
            Name = "WoodlandTestDriver";
            var timer = new Timer { WaitTime = 0.1, Autostart = true };
            timer.Timeout += Tick;
            AddChild(timer);
        }

        private void Tick()
        {
            if (step < 0)
                return;
            elapsed += 0.1;
            if (elapsed > 180)
            {
                Check(false, $"timeout at woodland step {step}");
                Finish();
                return;
            }
            try { RunStep(); }
            catch (Exception ex)
            {
                Check(false, $"step {step}: {ex}");
                Finish();
            }
        }

        private void RunStep()
        {
            // The early DialogBox lookup trap is avoided: wait for a current scene first.
            var scene = GetTree().Root.GetNodeOrNull<SceneManager>("SceneManager")?.CurrentSceneName;
            if (!Boot.IsReady || string.IsNullOrEmpty(scene))
                return;
            if (step == 0)
            {
                choices.Enqueue(0); // warrior
                choices.Enqueue(1); // pass the seer, no dice dependency
                Next();
            }
            if (AdvanceDialog() || GodotSceneService.IsTransitioning)
                return;
            if (++ticks < 3)
                return;
            switch (step)
            {
                case 1:
                    if (View?.Map.ID != "rpg_village") return;
                    if (!CaptureVillage()) return;
                    CheckTrees();
                    Check(View.FindEvent("woodland_workbench").Current.Tile == 39, "workbench has its own sprite instead of a barrel");
                    Check(Quest("ward").Status == QuestStatus.Progress, "journal starts in the village");
                    Go("rpg_elder_house", 6, 4, RpgDirection.Up);
                    Next(); return;
                case 2:
                    if (View?.Map.ID != "rpg_elder_house") return;
                    choices.Enqueue(0);
                    Tap(Key.E); Next(); return;
                case 3:
                    Check(Flag("rpg_quest_taken") == "1", "elder explains the ward and wolf");
                    Check(Quest("ward").Data.Status == 1, "journal points to the forest");
                    Go("rpg_forest", 9, 8, RpgDirection.Right); Next(); return;
                case 4:
                    if (View?.Map.ID != "rpg_forest") return;
                    CheckTrees();
                    Shot("forest");
                    View.Map.Encounters = null;
                    Tap(Key.E); Next(); return;
                case 5:
                    if (!Win("rpg_slime")) return;
                    Next(); return;
                case 6:
                    if (!ContinueVictory()) return;
                    Next(); return;
                case 7:
                    if (View?.Map.ID != "rpg_forest") return;
                    Check(Bag.CheckCount("rpg_plus/resin") >= 1, "slime loot is granted by the victory screen");
                    View.PlacePlayer(24, 8, RpgDirection.Right);
                    Tap(Key.E); Next(); return;
                case 8:
                    if (!Win("rpg_wolf", "rpg_slime")) return;
                    Next(); return;
                case 9:
                    if (!ContinueVictory()) return;
                    Next(); return;
                case 10:
                    if (View?.Map.ID != "rpg_forest") return;
                    if (recoveringAmber)
                    {
                        Check(Bag.CheckCount("rpg_plus/amber") == 1, "old save recovers exactly one missing amber through the cave pickup");
                        Check(!View.FindEvent("woodland_lost_amber").Visible, "recovered amber cannot be picked twice");
                        Bag.Add("rpg_demo/mushroom", 1);
                        Bag.Add("rpg_demo/branches", 2);
                        Go("rpg_village", 2, 12, RpgDirection.Right); Next(); return;
                    }
                    Check(Flag("rpg_wolf_dead") == "1", "wolf victory changes the world");
                    Check(Bag.CheckCount("rpg_plus/amber") == 1, "wolf drops the guaranteed quest amber");
                    Check(Bag.CheckCount("rpg_plus/wolf_pelt") >= 2, "wolf drops enough fur for the mantle");
                    Check(!View.FindEvent("woodland_lost_amber").Visible, "normal victory does not duplicate the amber");
                    Bag.Remove("rpg_plus/amber", 1);
                    View.RefreshEvents();
                    Check(View.FindEvent("woodland_lost_amber").Visible, "missing quest amber remains recoverable after an old wolf victory");
                    View.PlacePlayer(24, 8, RpgDirection.Right);
                    recoveringAmber = true;
                    Tap(Key.E); ticks = 0; return;
                case 11:
                    if (View?.Map.ID != "rpg_village") return;
                    Tap(Key.E); Next(); return;
                case 12:
                    Check(Quest("apothecary").Status == QuestStatus.Progress, "herbalist requests salve");
                    View.PlacePlayer(17, 13, RpgDirection.Up);
                    Tap(Key.E); Next(); return;
                case 13:
                    if (CraftWindow() == null) return;
                    resinBefore = Bag.CheckCount("rpg_plus/resin");
                    CraftRecipe("rpg_plus/salve");
                    Next(); return;
                case 14:
                    Check(Bag.CheckCount("rpg_plus/salve") == 1, "crafting UI creates the salve");
                    Check(Bag.CheckCount("rpg_plus/resin") == resinBefore - 1, "salve consumes one resin");
                    Check(RpgInput.IsWalkBlocked(this), "crafting overlay blocks movement");
                    Shot("crafting");
                    Tap(Key.Escape); Next(); return;
                case 15:
                    if (CraftWindow() != null) return;
                    View.PlacePlayer(2, 12, RpgDirection.Right);
                    choices.Enqueue(0);
                    Tap(Key.E); Next(); return;
                case 16:
                    Check(Quest("apothecary").Status == QuestStatus.End, "salve delivery finishes the first side quest");
                    Check(Bag.CheckCount("rpg_plus/salve") == 0, "delivery consumes the salve");
                    Go("rpg_forest", 5, 14, RpgDirection.Up); Next(); return;
                case 17:
                    if (View?.Map.ID != "rpg_forest") return;
                    Tap(Key.E); Next(); return;
                case 18:
                    Check(Flag("rpg_courier_found") == "1", "courier provides a note");
                    Check(Quest("courier").Data.Status == 1, "courier journal points back to the herbalist");
                    Go("rpg_village", 2, 12, RpgDirection.Right); Next(); return;
                case 19:
                    if (View?.Map.ID != "rpg_village") return;
                    Tap(Key.E); Next(); return;
                case 20:
                    Check(Quest("courier").Status == QuestStatus.End, "courier delivery closes the quest chain");
                    Go("rpg_elder_house", 6, 4, RpgDirection.Up); Next(); return;
                case 21:
                    if (View?.Map.ID != "rpg_elder_house") return;
                    Tap(Key.E); Next(); return;
                case 22:
                    Check(Quest("ward").Data.Status == 2, "elder supplies the lantern recipe objective");
                    Go("rpg_village", 17, 13, RpgDirection.Up); Next(); return;
                case 23:
                    if (View?.Map.ID != "rpg_village") return;
                    Tap(Key.E); Next(); return;
                case 24:
                    if (CraftWindow() == null) return;
                    CraftRecipe("rpg_plus/lantern"); Next(); return;
                case 25:
                    Check(Bag.CheckCount("rpg_plus/ward_lantern") == 1 && Bag.CheckCount("rpg_plus/amber") == 0, "lantern consumes amber and appears in the bag");
                    CraftRecipe("rpg_plus/mantle"); Next(); return;
                case 26:
                    Check(Bag.CheckCount("rpg_plus/fur_mantle") == 1, "loot also crafts usable equipment");
                    var defence = StatsService.Sheet("rpg_hero").Get("def");
                    Check(EquipmentService.Equip("rpg_hero", "rpg_plus/fur_mantle"), "crafted mantle equips");
                    Check(StatsService.Sheet("rpg_hero").Get("def") > defence, "crafted equipment changes combat stats");
                    Check(EquipmentService.ItemIn("rpg_hero", "body") == "rpg_plus/fur_mantle", "mantle uses the body slot before saving");
                    Tap(Key.Escape); Next(); return;
                case 27:
                    if (CraftWindow() != null) return;
                    Go("rpg_forest", 25, 8, RpgDirection.Right); Next(); return;
                case 28:
                    if (View?.Map.ID != "rpg_forest") return;
                    Tap(Key.E); Next(); return;
                case 29:
                    if (View?.Map.ID != "rpg_woodland_shrine") return;
                    CheckTrees();
                    Check(!View.CanEnter(new Vector2I(12, 4)) && !View.CanEnter(new Vector2I(10, 4)) && !View.CanEnter(new Vector2I(13, 4)), "altar base and stone piers block walking");
                    Check(!View.CanEnter(new Vector2I(12, 5)) && View.CanEnter(new Vector2I(12, 6)), "beacon is solid with a reachable interaction cell");
                    Check(Quest("ward").Data.Status == 3, "cave leads to the shrine and next journal stage");
                    View.PlacePlayer(12, 6, RpgDirection.Up);
                    choices.Enqueue(pact ? 1 : 0);
                    Tap(Key.E); Next(); return;
                case 30:
                    Check(Flag("rpg_ward_choice") == (pact ? "pact" : "free"), "chosen ending reaches state");
                    Check(Bag.CheckCount("rpg_plus/ward_lantern") == 0, "shrine consumes exactly one lantern");
                    Check(View.FindEvent("shrine_beacon").Current.Light.Radius == 6, "event page relights the shrine");
                    Check(View.FindEvent("shrine_beacon").Current.Tile == (pact ? 36 : 38), "ending changes the beacon appearance");
                    Shot("shrine");
                    savedResin = Bag.CheckCount("rpg_plus/resin");
                    SaveService.Save(SaveSlot);
                    GameSession.OpenMainMenu(GetTree()); Next(); return;
                case 31:
                    if (RpgWorldHost.Current != null) return;
                    Check(GameSession.LoadAndEnter(this, SaveSlot), "woodland save loads");
                    Next(); return;
                case 32:
                    if (View?.Map.ID != "rpg_woodland_shrine") return;
                    Check(Flag("rpg_ward_done") == "1", "shrine choice survives save/load");
                    Check(Flag("rpg_ward_choice") == (pact ? "pact" : "free"), "specific ending survives save/load");
                    Check(View.FindEvent("shrine_beacon").Current.Tile == (pact ? 36 : 38), "beacon appearance survives save/load");
                    Check(Quest("ward").Data.Status == 4, "journal stage survives save/load");
                    Check(Bag.CheckCount("rpg_plus/resin") == savedResin, "crafted inventory survives save/load");
                    Check(EquipmentService.ItemIn("rpg_hero", "body") == "rpg_plus/fur_mantle", "crafted gear survives save/load");
                    Go("rpg_elder_house", 6, 4, RpgDirection.Up); Next(); return;
                case 33:
                    if (View?.Map.ID != "rpg_elder_house") return;
                    paidCoins = Bag.CheckCount("money/coin");
                    Tap(Key.E); Next(); return;
                case 34:
                    Check(Quest("ward").Status == QuestStatus.End, "main quest completes");
                    Check(Bag.CheckCount("money/coin") == paidCoins + 35, "elder pays the promised reward");
                    paidCoins = Bag.CheckCount("money/coin");
                    Tap(Key.E); Next(); return;
                case 35:
                    Check(Bag.CheckCount("money/coin") == paidCoins, "repeat interaction cannot duplicate the reward");
                    SaveService.DeleteSave(SaveSlot);
                    Finish(); return;
            }
        }

        private bool AdvanceDialog()
        {
            if (Box?.IsRunning != true) return false;
            if (Box.IsWaitingAnswer) Box.TrySelectVariant(choices.Count > 0 ? choices.Dequeue() : 0);
            else Box.NextAction();
            return true;
        }

        private bool Win(params string[] ids)
        {
            if (GetTree().CurrentScene is not BattleSceneContext battle)
                return false;
            if (battle.IsDestroyed)
                return true;
            if (battle.ActiveBattleCore is not IBattleAutoPlayable auto || !auto.IsWaitingForPlayerInput || Time.GetTicksMsec() < nextAttack)
                return false;
            var enemies = battle.FindChild("FFEnemyPanel", true, false) as FFEnemyPanel;
            var players = battle.FindChild("FFPlayerPanel", true, false) as FFPlayerPanel;
            var menu = battle.FindChildren("*", "", true, false).OfType<FFMenuPanel>().FirstOrDefault();
            if (enemies == null || players == null || menu == null) return false;
            if (testedBattle != battle)
            {
                testedBattle = battle;
                victoryShot = ids.Contains("rpg_wolf") ? "wolf_loot" : "slime_loot";
                Check(enemies.Widgets.Select(w => w.Card.ID).OrderBy(id => id).SequenceEqual(ids.OrderBy(id => id)), "battle contains the expected monsters");
                foreach (var enemy in enemies.Widgets)
                {
                    enemy.Card.Attributes.Defence.Value = 0;
                    enemy.Card.Attributes.Health.Value = 1;
                }
            }
            var target = enemies.Widgets.FirstOrDefault(w => !w.IsDead);
            var player = players.Widgets.FirstOrDefault(w => !w.IsDead);
            if (target == null || player == null) return false;
            player.Card.Attributes.Point.Value = 100;
            Click(player);
            menu.SelectedIndex = menu.Entries.FindIndex(e => e.Key == "attack");
            menu.ConfirmSelection();
            menu.SelectedIndex = menu.Entries.FindIndex(e => e.Data is Cthangover.Core.Characters.ActionCharacter action && action.Type == Cthangover.Core.Characters.ActionCharacterType.ToEnemy);
            menu.ConfirmSelection();
            Click(target);
            nextAttack = Time.GetTicksMsec() + 1600;
            return false;
        }

        private static void Click(Control control)
            => control.EmitSignal(Control.SignalName.GuiInput, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true });

        private bool ContinueVictory()
        {
            if (GetTree().Root.FindChild("ContinueButton", true, false) is not Button button || !button.IsVisibleInTree()) return false;
            Shot(victoryShot);
            button.EmitSignal(BaseButton.SignalName.Pressed);
            return true;
        }

        private static void Go(string map, int x, int y, RpgDirection dir)
            => RpgWorldHost.Teleport(new RpgTeleport { Map = map, X = x, Y = y, Dir = dir });

        private static CraftPanel CraftWindow()
        {
            var host = UiHost.Instance;
            if (host == null || !host.TryGetInstance(CraftPanel.EntryId, out var node) || !node.IsVisibleInTree())
                return null;
            return node as CraftPanel;
        }

        private void CraftRecipe(string id)
        {
            var panel = CraftWindow();
            foreach (var row in panel.GetNode<VBoxContainer>("Panel/Margin/Root/Body/Scroll/Recipes").GetChildren().OfType<CraftRecipeButton>())
                if (row.RecipeId == id)
                {
                    row.EmitSignal(BaseButton.SignalName.Pressed);
                    break;
                }
            var button = panel.GetNode<Button>("Panel/Margin/Root/Body/Details/Craft");
            Check(!button.Disabled, $"recipe {id} has enough materials");
            if (!button.Disabled) button.EmitSignal(BaseButton.SignalName.Pressed);
        }

        private static void Tap(Key key)
        {
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
        }

        private void Next() { step++; ticks = 0; }

        private void CheckTrees()
        {
            var grid = RpgMapGrid.Parse(View.Map);
            var complete = true;
            var rootsSolid = true;
            var fragments = 0;
            var roots = 0;
            var invisibleWalls = 0;
            for (var y = 0; y < grid.Height; y++)
                for (var x = 0; x < grid.Width; x++)
                {
                    if (x > 0 && y > 0 && x < grid.Width-1 && y < grid.Height-1
                        && grid.GetCollision(x, y) == RpgCollision.Block
                        && grid.GetTile(RpgLayer.Main, x, y) < 0 && grid.GetTile(RpgLayer.Front, x, y) < 0
                        && !View.Tileset.IsSolid(grid.GetTile(RpgLayer.Back, x, y)))
                        invisibleWalls++;
                    foreach (var layer in new[] { RpgLayer.Main, RpgLayer.Front })
                    {
                        var tile = grid.GetTile(layer, x, y);
                        if (tile < 64 || tile > 72) continue;
                        fragments++;
                        if (tile != 71) continue;
                        roots++;
                        rootsSolid &= !View.CanEnter(new Vector2I(x, y));
                        for (var dy = 0; dy < 3; dy++)
                            for (var dx = 0; dx < 3; dx++)
                                complete &= grid.GetTile(dy == 2 ? RpgLayer.Main : RpgLayer.Front, x-1+dx, y-2+dy) == 64+dy*3+dx;
                    }
                }
            Check(roots > 0 && complete && fragments == roots * 9, View.Map.ID + " has complete trees without orphan fragments");
            Check(rootsSolid, View.Map.ID + " tree trunks block walking");
            Check(invisibleWalls == 0, View.Map.ID + " has no invisible interior collision cells");
        }

        private void Check(bool condition, string message)
        {
            if (condition) passed++; else failed++;
            GameLogger.Log("TEST", (condition ? "[PASS] " : "[FAIL] ") + message, condition ? LogLevel.Message : LogLevel.Error);
        }

        private bool CaptureVillage()
        {
            if (string.IsNullOrEmpty(shots))
                return true;
            var time = GameData.Instance.Runtime.Time;
            if (villageShotPhase == 0)
            {
                Shot("village_night");
                shotTime = time.Tick;
                time.SetTime(shotTime - shotTime % (24 * 60) + 12 * 60);
                villageShotPhase = 1;
                ticks = 0;
                return false;
            }
            if (villageShotPhase == 1)
            {
                Shot("village");
                time.SetTime(shotTime);
                villageShotPhase = 2;
            }
            return true;
        }

        private void Shot(string name)
        {
            if (string.IsNullOrEmpty(shots) || DisplayServer.GetName() == "headless") return;
            DirAccess.MakeDirRecursiveAbsolute(shots);
            ViewportCapture.SavePng(GetViewport(), shots.TrimEnd('/') + "/woodland_" + name + ".png");
        }

        private void Finish()
        {
            step = -1;
            GameLogger.Log("TEST", $"[TEST_RESULT] name={TestName} passed={passed} failed={failed}");
            GetTree().Quit(failed == 0 ? 0 : 1);
        }
    }
}
