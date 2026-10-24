using System.Collections.Generic;
using Cthangover.Core.Battle;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Characters;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Games;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.Items;
using Cthangover.Core.Utils;
using Cthangover.Mods.Interface.View;
using Godot;

namespace Cthangover.Mods.Rpg.Tests
{
    /// <summary>
    /// Plays the rpg_demo village like a player: key events go through
    /// <see cref="Input.ParseInputEvent"/>, so the world reads them exactly as
    /// it reads a keyboard (physical keys, held state, unhandled action key).
    /// <para/>
    /// A pickup is checked across map rebuilds: the suite's own
    /// <c>map_events/rpg_village/rpg_walk_test.json</c> lays a mushroom on the demo's
    /// village (an event added by another mod, shaped like the map editor's
    /// pickups); it is picked up, the player leaves the map and comes back, then
    /// saves, loads and comes back again — it must never reappear nor give a
    /// second item.
    /// <para/>
    /// One step per timer tick; a step either waits (returns without
    /// advancing) or checks and moves on. A failed check is recorded and the
    /// run goes on; an exception ends it. Result line:
    /// <c>[TEST_RESULT] name=rpg_walk passed=N failed=M</c>, exit code 0/1, and a
    /// 90 s watchdog.
    /// </summary>
    public partial class RpgWalkTestDriver : Node
    {
        private const float TickSeconds = 0.1f;

        // The battle opens with an intro animation (the backdrop zooms in): shots wait it out.
        private const int BattleIntroTicks = 20;
        private const float TimeoutSeconds = 90f;
        private const string Village = "rpg_village";
        private const string House = "rpg_elder_house";
        private const string WinFlag = "rpg_test_win";
        private const string SaveSlot = "slot_9901";
        private const string PickupId = "rpg_walk_mushroom";
        private const string PickupFlag = "rpg_village_rpg_walk_mushroom_taken";
        private const string Mushroom = "rpg_demo/mushroom";

        private static readonly Vector2I PickupCell = new(18, 14);

        private readonly string shotsDir;
        private readonly List<string> failures = new();
        private int passed;
        private int step;
        private int waitTicks;
        private int mushroomsBefore;
        private double elapsed;

        /// <summary>Creates the driver; <paramref name="shotsDir"/> null disables screenshots.</summary>
        public RpgWalkTestDriver(string shotsDir)
        {
            this.shotsDir = shotsDir;
            Name = "RpgWalkTestDriver";
            var timer = new Timer { WaitTime = TickSeconds, Autostart = true };
            AddChild(timer);
            timer.Timeout += Tick;
        }

        private static RpgWorldView View => RpgWorldHost.Current?.View;

        private static IInventory Inventory => GameData.Instance.Runtime.Inventory;

        private static string Flag(string name) => GameData.Instance.Runtime.Flags.TryGetValue(name, out var value) ? value : null;

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
                    // The intro dialog: the action key advances it.
                    if (AdvanceDialog())
                        return;
                    Check(GameData.Instance.Runtime.CharacterData.Characters.ContainsKey("rpg_hero"), "party.add put the hero into the party");
                    Check(View.Player.Cell == new Vector2I(15, 14), $"player at the spawn (15,14), got {View.Player.Cell}");
                    CheckActionTexts();
                    Shot("village");
                    Press(Key.W, true);
                    Next();
                    return;
                case 2:
                    if (View.Player.Cell.Y > 12)
                        return;
                    Press(Key.W, false);
                    Next();
                    return;
                case 3:
                    if (View.Player.IsMoving)
                        return;
                    Check(View.Player.Cell.X == 15 && View.Player.Cell.Y <= 12, $"W walked up the path, at {View.Player.Cell}");
                    Check(RpgState.Instance.Data.Map == Village && RpgState.Instance.Data.Y == View.Player.Cell.Y, "the position is stored after the step");
                    View.PlacePlayer(14, 11, RpgDirection.Up);
                    Press(Key.S, true);
                    Next();
                    return;
                case 4:
                    if (++waitTicks < 4)
                        return;
                    Press(Key.S, false);
                    Check(View.Player.Cell == new Vector2I(14, 11), $"the well blocks the way, at {View.Player.Cell}");
                    Check(View.Player.Facing == RpgDirection.Down, "a blocked key still turns the player");
                    Next();
                    return;
                case 5:
                    // The S release is read on the next frame; placing the player
                    // before it would let the still-held key walk them away.
                    View.PlacePlayer(16, 12, RpgDirection.Up);
                    Tap(Key.E);
                    Next();
                    return;
                case 6:
                    Check(RpgInput.FindDialogBox()?.IsRunning == true, "E on the sign opens a dialog");
                    Next();
                    return;
                case 7:
                    if (AdvanceDialog())
                        return;
                    View.PlacePlayer(10, 10, RpgDirection.Left);
                    Tap(Key.F);
                    Next();
                    return;
                case 8:
                    Check(RpgInput.FindDialogBox()?.IsRunning == true, "F talks to the villager");
                    Check(View.FindEvent("villager")?.Facing == RpgDirection.Right, "the villager turns to the player");
                    Next();
                    return;
                case 9:
                    if (AdvanceDialog())
                        return;
                    // A pickup this suite adds to the demo's village (map_events of another mod).
                    mushroomsBefore = Inventory.CheckCount(Mushroom);
                    Check(View.FindEvent(PickupId)?.Visible == true, "a test mod's map_events file adds a pickup to the demo's village");
                    View.PlacePlayer(PickupCell.X - 1, PickupCell.Y, RpgDirection.Right);
                    Press(Key.D, true);
                    Next();
                    return;
                case 10:
                    if (View.Player.Cell.X < PickupCell.X)
                        return;
                    Press(Key.D, false);
                    Next();
                    return;
                case 11:
                    // Stepping on the pickup starts its dialog; the flag is set when it ends.
                    if (AdvanceDialog() || Flag(PickupFlag) != "1")
                        return;
                    Check(Inventory.CheckCount(Mushroom) == mushroomsBefore + 1, $"stepping on the pickup adds the item, {Inventory.CheckCount(Mushroom)} now");
                    Check(View.FindEvent(PickupId)?.Visible == false, "the picked item leaves the map");
                    RpgWorldHost.Teleport(new RpgTeleport { Map = House, X = 6, Y = 8, Dir = RpgDirection.Up });
                    Next();
                    return;
                case 12:
                    if (View?.Map?.ID != House || GodotSceneService.IsTransitioning || AdvanceDialog())
                        return;
                    RpgWorldHost.Teleport(new RpgTeleport { Map = Village, X = PickupCell.X - 1, Y = PickupCell.Y, Dir = RpgDirection.Right });
                    Next();
                    return;
                case 13:
                    if (View?.Map?.ID != Village || GodotSceneService.IsTransitioning || AdvanceDialog())
                        return;
                    CheckPickupGone("after leaving the map and coming back");
                    Press(Key.D, true);
                    Next();
                    return;
                case 14:
                    if (View.Player.Cell.X < PickupCell.X)
                        return;
                    Press(Key.D, false);
                    Next();
                    return;
                case 15:
                    if (View.Player.IsMoving || ++waitTicks < 5)
                        return;
                    Check(RpgInput.FindDialogBox()?.IsRunning != true && Inventory.CheckCount(Mushroom) == mushroomsBefore + 1,
                        "stepping on the empty cell again gives nothing");
                    View.PlacePlayer(6, 7, RpgDirection.Up);
                    Press(Key.W, true);
                    Next();
                    return;
                case 16:
                    if (!View.Player.IsMoving && View.Map.ID == Village)
                        return;
                    Press(Key.W, false);
                    Next();
                    return;
                case 17:
                    if (View?.Map?.ID != House)
                        return;
                    if (AdvanceDialog())
                        return;
                    Check(View.Player.Cell == new Vector2I(6, 8), $"the door teleports into the house at (6,8), got {View.Player.Cell}");
                    Check(View.FindEvent("elder_quest")?.Visible == true && View.FindEvent("elder_thanks")?.Visible == false, "before the wolf the elder gives the quest");
                    Shot("house");
                    ScenarioCondition.SetFlag("rpg_wolf_dead", "1");
                    Next();
                    return;
                case 18:
                    Check(View.FindEvent("elder_quest")?.Visible == false && View.FindEvent("elder_thanks")?.Visible == true, "a story flag swaps the elder's events at once");
                    ScenarioCondition.SetFlag("rpg_wolf_dead", "0");
                    Check(RpgBattleService.Start("rpg_slime", WinFlag), "a battle starts from the map");
                    Godot.Engine.TimeScale = 4;
                    Next();
                    return;
                case 19:
                    if (GetTree().CurrentScene is not BattleSceneContext battle)
                        return;
                    // The core's auto turn only passes, so the hero would lose: once the
                    // core waits for the player, take the platform's victory path.
                    if (battle.ActiveBattleCore is not IBattleAutoPlayable auto || !auto.IsWaitingForPlayerInput)
                        return;
                    // The shot waits for the fade-in and the intro animation, otherwise it catches the zoomed backdrop.
                    if (GodotSceneService.IsTransitioning || ++waitTicks < BattleIntroTicks)
                        return;
                    Shot("battle");
                    battle.ShowWinground();
                    Next();
                    return;
                case 20:
                    if (GetTree().Root.FindChild("ContinueButton", true, false) is not Button button || !button.IsVisibleInTree())
                        return;
                    Shot("victory");
                    button.EmitSignal(BaseButton.SignalName.Pressed);
                    Next();
                    return;
                case 21:
                    if (View?.Map?.ID != House || GodotSceneService.IsTransitioning)
                        return;
                    Godot.Engine.TimeScale = 1;
                    Check(View.Player.Cell == new Vector2I(6, 8), $"the battle returns to the same cell, got {View.Player.Cell}");
                    Check(GameData.Instance.Runtime.Flags.TryGetValue(WinFlag, out var won) && won == "1", "the win flag is set after the victory");
                    // The menu's path: save in the game, go to the main menu, load.
                    SaveService.Save(SaveSlot);
                    GameSession.OpenMainMenu(GetTree());
                    Next();
                    return;
                case 22:
                    if (RpgWorldHost.Current != null || ++waitTicks < 5)
                        return;
                    Check(GameSession.LoadAndEnter(this, SaveSlot), "the save loads");
                    Next();
                    return;
                case 23:
                    if (View?.Map?.ID != House || GodotSceneService.IsTransitioning || ++waitTicks < 5)
                        return;
                    Shot("loaded");
                    var dialog = RpgInput.FindDialogBox();
                    Check(dialog != null && !dialog.IsRunning && !dialog.IsVisibleInTree(), $"no dialog box after loading (running={dialog?.IsRunning}, visible={dialog?.IsVisibleInTree()})");
                    Check(View.Player.Cell == new Vector2I(6, 8), $"the loaded game puts the player on the saved cell, got {View.Player.Cell}");
                    Check(Flag(PickupFlag) == "1" && Inventory.CheckCount(Mushroom) == mushroomsBefore + 1, "the save keeps the pickup's flag and the picked item");
                    RpgWorldHost.Teleport(new RpgTeleport { Map = Village, X = PickupCell.X - 1, Y = PickupCell.Y, Dir = RpgDirection.Right });
                    Next();
                    return;
                case 24:
                    if (View?.Map?.ID != Village || GodotSceneService.IsTransitioning || AdvanceDialog())
                        return;
                    CheckPickupGone("after loading the save");
                    SaveService.DeleteSave(SaveSlot);
                    Finish();
                    return;
            }
        }

        /// <summary>
        /// The battle shows the names and descriptions of the hero's action cards:
        /// their keys must be translated by a mod of this game, not only by
        /// another game's content mod.
        /// </summary>
        private void CheckActionTexts()
        {
            var hero = CharacterFactory.Instance.Get("rpg_hero");
            Check(hero?.Actions?.Count > 0, "the hero has action cards");
            foreach (var action in hero?.Actions ?? new List<ActionCharacter>())
            {
                foreach (var key in new[] { action.Name, action.Description })
                    Check(!string.IsNullOrEmpty(key) && TranslationServer.Translate(key) != key, $"action text '{key}' is translated");
            }
        }

        /// <summary>The map was built again (a teleport or a load): the picked item must stay gone and give nothing.</summary>
        private void CheckPickupGone(string when)
        {
            var pickup = View.FindEvent(PickupId);
            Check(pickup != null && !pickup.Visible, $"the picked item does not come back {when}");
            Check(View.EventAt(PickupCell, RpgEventTrigger.Touch) == null, $"its cell has no pickup to step on {when}");
            Check(Inventory.CheckCount(Mushroom) == mushroomsBefore + 1, $"the inventory still holds exactly the one picked item {when}");
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

        private bool AdvanceDialog()
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
            ViewportCapture.SavePng(GetViewport(), shotsDir.TrimEnd('/') + "/rpg_" + name + ".png");
        }

        private void Finish()
        {
            step = -1;
            Godot.Engine.TimeScale = 1;
            GameLogger.Log("TEST", $"[TEST_RESULT] name=rpg_walk passed={passed} failed={failures.Count}");
            GetTree().Quit(failures.Count == 0 ? 0 : 1);
        }
    }
}
