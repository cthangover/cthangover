namespace Cthangover.Mods.RpgPlus.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Battle;
    using Cthangover.Core.Battle.Actions;
    using Cthangover.Core.Bootstrap;
    using Cthangover.Core.Characters;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Games;
    using Cthangover.Core.Items;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;
    using Cthangover.Core.UI;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Dialog;
    using Cthangover.Mods.Equipment;
    using Cthangover.Mods.Interface.View;
    using Cthangover.Mods.Items;
    using Cthangover.Mods.Rpg;
    using Cthangover.Mods.Stats;
    using Cthangover.Mods.Trading;
    using Godot;

    /// <summary>
    /// Plays the RPG Plus sample end to end, the way a player would, and checks that the
    /// mods agree with each other at every step: the prologue's class choice reaches the
    /// sheet, the sheet reaches the battle attributes, gear and tonics change both, a
    /// formula skill hits the stat-patched wolf, the training yard levels the hero up and
    /// teaches the class skill, the sheet panel takes gear off, and a save brings it all back.
    /// </summary>
    public partial class RpgPlusTestDriver : Node
    {
        private const float TickSeconds = 0.1f;

        // The battle opens with an intro animation (the backdrop zooms in): shots wait it out.
        private const int BattleIntroTicks = 20;
        private const float TimeoutSeconds = 150f;
        private const string Hero = "rpg_hero";
        private const string Crossroads = "rpg_plus_crossroads";
        private const string Yard = "rpg_plus_training_yard";
        private const string Village = "rpg_village";
        private const string Rogue = "classic/rogue";
        private const string Dagger = "classic/dagger";
        private const string IronSword = "classic/iron_sword";
        private const string Staff = "classic/oak_staff";
        private const string Tonic = "classic/strength_tonic";
        private const string Charm = "classic/clover_charm";
        private const string WinFlag = "rpg_plus_test_win";
        private const string SaveSlot = "slot_9902";
        private const string SheetEntry = "sheet";

        private readonly string shotsDir;
        private readonly List<string> failures = new();
        private readonly Queue<int> choices = new();
        private int passed;
        private int step;
        private int waitTicks;
        private int pagesPhase;
        private int nightPhase;
        private long savedTime;
        private double pagesWait;
        private bool slimeMoved;
        private int coinsBeforeChest;
        private double elapsed;
        private string checkValue;
        private string checkResult;
        private bool sawYard;

        public RpgPlusTestDriver(string shotsDir)
        {
            this.shotsDir = shotsDir;
            Name = "RpgPlusTestDriver";
            var timer = new Timer { WaitTime = TickSeconds, Autostart = true };
            AddChild(timer);
            timer.Timeout += Tick;
        }

        private static RpgWorldView View => RpgWorldHost.Current?.View;

        private static IInventory Inventory => GameData.Instance.Runtime.Inventory;

        private static CharacterInfoData Entry => StatsService.RosterEntry(Hero);

        private static DialogBox Box => RpgInput.FindDialogBox();

        private string SceneName => GetTree().Root.GetNodeOrNull<SceneManager>("SceneManager")?.CurrentSceneName;

        private static string Flag(string name) => GameData.Instance.Runtime.Flags.TryGetValue(name, out var value) ? value : null;

        private void Tick()
        {
            if (step < 0)
                return;
            elapsed += TickSeconds;
            if (elapsed > TimeoutSeconds)
            {
                Fail($"timeout at step {step} (scene {SceneName}, map {View?.Map?.ID}, dialog running {Box?.IsRunning})");
                Finish();
                return;
            }
            try
            {
                RunStep();
            }
            catch (System.Exception ex)
            {
                Fail($"step {step}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                Finish();
            }
        }

        private void RunStep()
        {
            switch (step)
            {
                case 0:
                    if (!Boot.IsReady || SceneName != Crossroads || Box?.IsRunning != true)
                        return;
                    Shot("prologue");
                    // The class question: the rogue; the seer: ask the way (the charisma check).
                    choices.Enqueue(2);
                    choices.Enqueue(0);
                    Next();
                    return;
                case 1:
                    RememberCheck();
                    if (AdvanceDialog())
                        return;
                    if (View?.Map?.ID != Village || GodotSceneService.IsTransitioning || Entry == null || ++waitTicks < 3)
                        return;
                    CheckPrologue();
                    CheckSheet();
                    Next();
                    return;
                case 2:
                    CheckGear();
                    CheckConditionsAndShop();
                    Check(RpgBattleService.Start("rpg_wolf", WinFlag), "a battle with the stat-patched wolf starts from the map");
                    Godot.Engine.TimeScale = 4;
                    Next();
                    return;
                case 3:
                    if (GetTree().CurrentScene is not BattleSceneContext battle)
                        return;
                    if (battle.ActiveBattleCore is not IBattleAutoPlayable auto || !auto.IsWaitingForPlayerInput || GodotSceneService.IsTransitioning || ++waitTicks < BattleIntroTicks)
                        return;
                    Shot("battle");
                    CheckBattle();
                    battle.ShowWinground();
                    Next();
                    return;
                case 4:
                    if (GetTree().Root.FindChild("ContinueButton", true, false) is not Button button || !button.IsVisibleInTree())
                        return;
                    button.EmitSignal(BaseButton.SignalName.Pressed);
                    Next();
                    return;
                case 5:
                    if (View?.Map?.ID != Village || GodotSceneService.IsTransitioning || AdvanceDialog())
                        return;
                    Godot.Engine.TimeScale = 1;
                    // The victory is taken by the platform's shortcut (the wolf is not killed), so it
                    // awards no exp; the exp path is the training yard below.
                    if (!CheckPagesAndRoute() || !CheckNight())
                        return;
                    Check(Flag(WinFlag) == "1", "the win flag is set after the victory");
                    // To the trainer, added to the demo's village by this mod's own map events file.
                    Check(View.FindEvent("rpg_plus_trainer")?.Visible == true, "the trainer stands in the demo's village");
                    View.PlacePlayer(26, 10, RpgDirection.Right);
                    choices.Enqueue(0); // the trainer: to the yard
                    choices.Enqueue(0); // the yard: drill
                    choices.Enqueue(2); // the yard: back to the village
                    Tap(Key.E);
                    Next();
                    return;
                case 6:
                    sawYard |= SceneName == Yard;
                    if (AdvanceDialog())
                        return;
                    if (!sawYard || View?.Map?.ID != Village || GodotSceneService.IsTransitioning || ++waitTicks < 3)
                        return;
                    CheckTraining();
                    Next();
                    return;
                case 7:
                    // The toolbar button's own path.
                    UiRegistry.Toggle(SheetEntry);
                    var panel = SheetPanel();
                    Check(panel != null && panel.Visible, "the toolbar entry 'sheet' opens the character sheet");
                    if (panel == null)
                    {
                        Finish();
                        return;
                    }
                    panel.Select(Hero);
                    Next();
                    return;
                case 8:
                    if (++waitTicks < 2)
                        return;
                    Shot("sheet_full");
                    CheckPanelAndTakeOff(SheetPanel());
                    Next();
                    return;
                case 9:
                    if (++waitTicks < 3)
                        return;
                    var sheetPanel = SheetPanel();
                    Check(EquipmentService.ItemIn(Hero, "weapon") == null, "Take off in the sheet empties the weapon slot");
                    Check(Inventory.CheckCount(IronSword) == 1, "the sword is back in the bag");
                    Check(sheetPanel.FindChild("Slot_weapon", true, false)?.FindChild("TakeOff", true, false) == null, "the redrawn sheet shows the slot empty");
                    Shot("sheet");
                    sheetPanel.Hide();
                    Check(EquipmentService.Equip(Hero, Dagger), "equip.put's service puts the dagger on from the bag");
                    SaveService.Save(SaveSlot);
                    GameSession.OpenMainMenu(GetTree());
                    Next();
                    return;
                case 10:
                    if (RpgWorldHost.Current != null || ++waitTicks < 5)
                        return;
                    Check(GameSession.LoadAndEnter(this, SaveSlot), "the save loads");
                    Next();
                    return;
                case 11:
                    if (View?.Map?.ID != Village || GodotSceneService.IsTransitioning || ++waitTicks < 5)
                        return;
                    CheckLoaded();
                    SaveService.DeleteSave(SaveSlot);
                    Finish();
                    return;
            }
        }

        private void RememberCheck()
        {
            var value = Box?.Runtime?.GetVariable("check_value");
            if (!string.IsNullOrEmpty(value))
            {
                checkValue = value;
                checkResult = Box.Runtime.GetVariable("check_result");
            }
        }

        private void CheckPrologue()
        {
            Check(StatsService.ArchetypeOf(Hero) == Rogue, $"the prologue's choice made the hero a rogue, got {StatsService.ArchetypeOf(Hero)}");
            Check(EquipmentService.ItemIn(Hero, "weapon") == Dagger, "the rogue starts with the dagger in the weapon slot");
            Check(Inventory.CheckCount("money/coin") >= 25, "the purse of the prologue is in the bag");
            var cha = StatsService.Sheet(Hero).Rounded("cha");
            Check(checkValue == cha.ToString(), $"the check line rolled the hero's charisma ({checkValue} vs {cha})");
            var success = checkResult == TranslationServer.Translate("checks/success").ToString();
            Check(success == Inventory.HasItem(Charm), $"the seer's charm follows the check's outcome ({checkResult})");
        }

        private void CheckSheet()
        {
            var sheet = StatsService.Sheet(Hero);
            Check(sheet.GetBase("agi") == 10 && sheet.Get("agi") == 12, $"rogue agility 8 + 2*level, +2 from the dagger: {sheet.GetBase("agi")} -> {sheet.Get("agi")}");
            Check(sheet.Rounded("atk") == 13, $"attack = str + level + agi/4 (rogue trait) + 3 (dagger) = 13, got {sheet.Get("atk")}");
            Check(Entry.Attributes.Attack.Value == 13 && Entry.Attributes.Attack.BaseValue == 13, $"the bound attack reached the battle attribute: {Entry.Attributes.Attack.Value}");
            Check(Entry.Attributes.Health.BaseValue == sheet.Rounded("hp") && sheet.Rounded("hp") == 29, $"max health 12 + vit*3 + level*2 = 29 reached the battle attribute: {Entry.Attributes.Health.BaseValue}");
            Check(Entry.ActionSlots?.Contains("classic/power_strike") == true, "the rogue's level 1 skill went into an action slot");
        }

        private void CheckGear()
        {
            Inventory.Add(IronSword, 1);
            Check(UseOnHero(IronSword), "the bag's equip action puts the iron sword on");
            Check(EquipmentService.ItemIn(Hero, "weapon") == IronSword, "the sword is in the weapon slot");
            Check(Inventory.CheckCount(Dagger) == 1 && Inventory.CheckCount(IronSword) == 0, "the dagger went back to the bag, the sword left it");
            var sheet = StatsService.Sheet(Hero);
            Check(sheet.Get("agi") == 10 && sheet.Rounded("atk") == 14, $"without the dagger: agi 10, atk 7 + 2 + 5 = 14, got {sheet.Get("agi")} / {sheet.Get("atk")}");
            Check(Entry.Attributes.Attack.Value == 14, "the battle attribute follows the gear change");

            Inventory.Add(Staff, 1);
            Check(!UseOnHero(Staff), "a mages-only staff refuses a rogue");
            Check(Inventory.CheckCount(Staff) == 1 && EquipmentService.ItemIn(Hero, "weapon") == IronSword, "the refused staff stays in the bag");

            Inventory.Add(Tonic, 1);
            Check(UseOnHero(Tonic), "the strength tonic is applied");
            Check(StatsService.Sheet(Hero).Rounded("str") == 7 && StatsService.Sheet(Hero).Rounded("atk") == 15, "the tonic raised strength for good and attack with it");
        }

        private void CheckConditionsAndShop()
        {
            Check(ScenarioCondition.Evaluate("equipped(rpg_hero, 'classic/iron_sword') && archetype(rpg_hero, 'classic/rogue') && stat(rpg_hero, str) >= 7 && level(rpg_hero) == 1"),
                "conditions read equipment, archetype, stats and level");
            Check(!ScenarioCondition.Evaluate("equipped(rpg_hero, 'classic/dagger')"), "a dagger in the bag is not worn");
            var shop = ShopFactory.Instance.Get("rpg_demo/village_merchant");
            Check(shop?.FindSell(IronSword)?.Price == 30, "the shop patch added the gear to the demo's merchant");
            Check(shop?.FindSell("rpg_demo/elixir") != null, "the merchant still sells his own elixir");
        }

        private void CheckBattle()
        {
            var wolf = BattleRequest.Current?.EnemiesCards?.FirstOrDefault(e => e?.ID == "rpg_wolf");
            Check(wolf != null, "the wolf is in the battle");
            if (wolf == null)
                return;
            var wolfSheet = StatsService.Sheet("rpg_wolf");
            Check(wolf.Attributes.Health.BaseValue == wolfSheet.Rounded("hp") && wolfSheet.Rounded("hp") == 31, $"the patched wolf fights with its sheet's health 31, got {wolf.Attributes.Health.BaseValue}");
            Check(wolf.Attributes.Defence.Value == 2, $"and its sheet's shield floor(vit/2) = 2, got {wolf.Attributes.Defence.Value}");
            var before = wolf.Attributes.Health.Value;
            var hero = new Character { ID = Hero, Level = StatsService.LevelOf(Hero), Attributes = Entry.Attributes };
            hero.Attributes.Point.Value = System.Math.Max(1, hero.Attributes.Point.Value);
            var result = ActionExecutorHub.Instance.Execute(ActionCharacterFactory.Instance.Get("classic/power_strike"), hero, wolf);
            Check(result.Result && result.Target.Damage > 0, $"the formula skill power strike hits, damage {result.Target.Damage}");
            Check(wolf.Attributes.Health.Value == before - result.Target.Damage, "the damage left the wolf's health");
            Check(result.Target.Defence == -2, $"the shield took its 2 first, change {result.Target.Defence}");

            // Mana as a bar of the battle core: battle_formulas gives the pool, ff_battle draws it.
            var mana = BattleUnitGauges.Read(hero).FirstOrDefault(g => g.Id == "mp");
            Check(mana != null && mana.Max == StatsService.Sheet(Hero).Rounded("mp") && mana.Current <= mana.Max, $"the hero has a mana gauge of the sheet's mp, got {mana?.Current}/{mana?.Max}");
            var bars = GetTree().Root.FindChildren("Gauges", "VBoxContainer", true, false).OfType<Control>().Where(c => c.IsVisibleInTree()).ToList();
            Check(bars.Any(b => b.FindChildren("Value", "Label", true, false).OfType<Label>().Any(l => l.Text == $"{mana?.Current}/{mana?.Max}")), "the FF party panel shows the mana bar with its numbers");
        }

        private void CheckTraining()
        {
            Check(Flag("rpg_plus_drilled") == "1", "the training yard's drill ran");
            Check(StatsService.LevelOf(Hero) == 2, $"20 exp from the drill took the hero to level 2, level {StatsService.LevelOf(Hero)}, exp {StatsService.ExpOf(Hero)}");
            var learned = Entry.ActionSlots?.Contains("classic/guard_break") == true || GameData.Instance.Runtime.ActionPool.ActionIds.Contains("classic/guard_break");
            Check(learned, "level 2 taught the rogue guard break (a slot or the action pool)");
            var sheet = StatsService.Sheet(Hero);
            Check(Entry.Attributes.Health.BaseValue == sheet.Rounded("hp") && sheet.Rounded("hp") == 34, $"max health grew with the level to 34, got {Entry.Attributes.Health.BaseValue}");
        }

        private void CheckPanelAndTakeOff(StatSheetPanel panel)
        {
            Check(panel.Visible && panel.SelectedId == Hero, "the sheet shows the hero");
            Check(panel.FindChild("Stat_agi", true, false) != null && panel.FindChild("Stat_hp", true, false) != null, "the sheet lists the pack's stats");
            Check(panel.FindChild("Section_equipment", true, false) != null, "the equipment mod's block is in the sheet");
            var takeOff = panel.FindChild("Slot_weapon", true, false)?.FindChild("TakeOff", true, false) as Button;
            Check(takeOff != null, "the worn sword has a Take off button");
            takeOff?.EmitSignal(BaseButton.SignalName.Pressed);
        }

        private void CheckLoaded()
        {
            Check(StatsService.ArchetypeOf(Hero) == Rogue, "the archetype survives the save");
            Check(EquipmentService.ItemIn(Hero, "weapon") == Dagger, "the worn dagger survives the save");
            Check(StatsService.LevelOf(Hero) == 2, "the level survives the save");
            var sheet = StatsService.Sheet(Hero);
            Check(StatState.Instance.Find(Hero)?.Bonus.GetValueOrDefault("str") == 1, "the tonic's permanent bonus survives the save");
            Check(sheet.Rounded("str") == 8, $"strength at level 2: 5 + 2 + 1 (tonic) = 8, got {sheet.Get("str")}");
            Check(Entry.Attributes.Attack.Value == sheet.Rounded("atk"), "the loaded battle attributes agree with the sheet");
        }

        private static bool UseOnHero(string itemId)
        {
            // What the inventory panel does after the player picks the hero as the target.
            var item = ItemFactory.Instance.Get(itemId);
            if (item?.ItemAction is not ITargetedItemAction targeted || !targeted.UseOn(item, Entry))
                return false;
            Inventory.Remove(item, 1);
            return true;
        }

        private static StatSheetPanel SheetPanel()
            => UiHost.Instance != null && UiHost.Instance.TryGetInstance(SheetEntry, out var control) ? control as StatSheetPanel : null;

        /// <summary>
        /// Event pages and a move route (rpg): the tame slime walks its route, the chest gives
        /// coins once and then shows its open page. True when done; runs over several ticks.
        /// </summary>
        private bool CheckPagesAndRoute()
        {
            var chest = View.FindEvent("rpg_plus_chest");
            var slime = View.FindEvent("rpg_plus_pet_slime");
            switch (pagesPhase)
            {
                case 0:
                    Check(chest?.Visible == true && chest.PageIndex == 0, "the closed chest shows its first page");
                    Check(slime?.Current?.Route != null, "the tame slime has a route");
                    pagesWait = 0;
                    pagesPhase++;
                    return false;
                case 1:
                    // Left, wait, right, right…: within three seconds it has left its cell once.
                    slimeMoved |= slime != null && slime.Cell != new Vector2I(6, 15);
                    pagesWait += TickSeconds;
                    if (pagesWait < 3)
                        return false;
                    Check(slimeMoved, "the slime walks its route on its own");
                    coinsBeforeChest = Inventory.CheckCount("money/coin");
                    View.PlacePlayer(28, 3, RpgDirection.Right);
                    Tap(Key.E);
                    pagesPhase++;
                    return false;
                case 2:
                    if (AdvanceDialog())
                        return false;
                    Check(Inventory.CheckCount("money/coin") == coinsBeforeChest + 10, "the chest gave ten coins");
                    Check(chest?.PageIndex == 1 && chest.Current.Tile == 15, "the flag switched the chest to its open page");
                    Check(chest?.Current?.Commands?.Count == 1, "the open page has its own commands");
                    pagesPhase++;
                    return true;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Light of the map (rpg): at 23:00 the layers and actors share one lit material with
        /// night weights, and the player's glow, the lamp and the windows of the village tileset
        /// are lit. Shot "night" next to the lamp; the clock is put back afterwards.
        /// </summary>
        private bool CheckNight()
        {
            var time = GameData.Instance.Runtime.Time;
            switch (nightPhase)
            {
                case 0:
                    savedTime = time.Tick;
                    time.SetTime(savedTime - savedTime % (24 * 60) + 23 * 60);
                    View.PlacePlayer(12, 9, RpgDirection.Up);
                    nightPhase++;
                    return false;
                case 1:
                    var material = View.GetNodeOrNull<TileMapLayer>("Main")?.Material as ShaderMaterial;
                    Check(material != null, "the tiles have the map's light material");
                    Check(material != null && View.Player.Material == material, "the player shares it");
                    var weights = material?.GetShaderParameter("time_weights").AsVector4() ?? Vector4.Zero;
                    Check(Mathf.IsEqualApprox(weights.W, 1f), "23:00 lights the map as night");
                    Check((material?.GetShaderParameter("light_count").AsInt32() ?? 0) >= 4, "the player, the lamp and windows give light");
                    Shot("night");
                    time.SetTime(savedTime);
                    nightPhase++;
                    return true;
                default:
                    return true;
            }
        }

        private bool AdvanceDialog()
        {
            var box = Box;
            if (box == null || !box.IsRunning)
                return false;
            if (box.IsWaitingAnswer)
                box.TrySelectVariant(choices.Count > 0 ? choices.Dequeue() : 0);
            else
                box.NextAction();
            return true;
        }

        private static void Tap(Key key)
        {
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { PhysicalKeycode = key, Keycode = key, Pressed = false });
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
            ViewportCapture.SavePng(GetViewport(), shotsDir.TrimEnd('/') + "/rpg_plus_" + name + ".png");
        }

        private void Finish()
        {
            step = -1;
            Godot.Engine.TimeScale = 1;
            GameLogger.Log("TEST", $"[TEST_RESULT] name=rpg_plus passed={passed} failed={failures.Count}");
            GetTree().Quit(failures.Count == 0 ? 0 : 1);
        }
    }
}
