using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Events;
using Cthangover.Core.Games;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.HotReload;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.NewGameTest
{
    /// <summary>
    /// Plays the games the way a player does, in ONE process: no restart
    /// happens anywhere, every game switch replaces the mod set in place.
    /// <list type="number">
    /// <item>Front-end menu → Play → the <c>cthangover</c> card → New game: the
    /// process switches to the game, enters <c>prolog_alley</c> and the opening
    /// dialog about the hungover Marao is clicked through.</item>
    /// <item>Hot reload inside the game (forced reload of <c>starter_party</c>):
    /// the session is parked, the mod reloads into a new context and the game
    /// comes back in <c>prolog_alley</c>.</item>
    /// <item>Back to the menu → <c>card_novel</c>: mods outside that game are
    /// unloaded and a scene of the game is entered.</item>
    /// <item>Back to the menu → <c>cthangover</c> again → <c>prolog_alley</c>.</item>
    /// </list>
    /// After every switch the released mod contexts must be collected
    /// (<c>ModManager.LastReload.Leaked</c> empty). Scene entries are taken from
    /// <see cref="SceneEnteredEvent"/>, not by polling the current scene name: a
    /// game's entry scene may move on to the next scene within a frame.
    /// Prints <c>[TEST_RESULT] name=new_game</c>.
    /// </summary>
    public partial class NewGameTestDriverNode : Node
    {
        public const string Flag = "--itest=new_game";

        private const string MainGame = "cthangover";
        private const string SecondGame = "card_novel";
        private const string PrologScene = "prolog_alley";
        private const string HotReloadedMod = "starter_party";
        private const float TimeoutSeconds = 420f;

        private readonly List<string> failures = new();
        private readonly List<string> enteredScenes = new();
        private int passed;
        private int step;
        private double elapsed;
        private double stepAt;
        private bool done;
        private bool sawMarao;
        private int clicks;
        private bool hotReloadDone;

        public override void _Ready()
        {
            // The driver must keep ticking across every mod-set switch of the run.
            ProcessMode = ProcessModeEnum.Always;
            ModEventBus.Subscribe<SceneEnteredEvent>(OnSceneEntered);
        }

        public override void _ExitTree()
        {
            ModEventBus.Unsubscribe<SceneEnteredEvent>(OnSceneEntered);
        }

        public override void _Process(double delta)
        {
            if (done)
                return;

            elapsed += delta;
            if (elapsed > TimeoutSeconds)
            {
                failures.Add($"timeout at step {step} (scene='{SceneManager.GetCurrentSceneName()}', game='{ActiveGame.Id}')");
                Finish();
                return;
            }

            if (elapsed - stepAt < 0.25)
                return;
            stepAt = elapsed;

            try
            {
                RunStep();
            }
            catch (Exception ex)
            {
                failures.Add($"step {step}: {ex.GetType().Name}: {ex.Message}");
                Finish();
            }
        }

        private Control Menu => GetTree().CurrentScene as Control;

        private static bool Busy => GameSession.IsSwitching || ModManager.Instance.IsReloading || ModHotReloadService.IsReloadRunning;

        private void OnSceneEntered(SceneEnteredEvent e) => enteredScenes.Add(e.SceneName);

        private void RunStep()
        {
            switch (step)
            {
                case 0:
                    if (!Boot.IsReady || !PressMenuPlay())
                        return;
                    Check("front-end menu offers Play", ActiveGame.IsFrontendOnly);
                    break;

                case 1:
                    if (!StartFromPicker(MainGame))
                        return;
                    break;

                case 2:
                    if (Busy || ActiveGame.Id != MainGame || !enteredScenes.Contains(PrologScene))
                        return;
                    CheckSwitch(MainGame, expectUnload: false);
                    Check($"entered '{PrologScene}'", true);
                    break;

                case 3:
                {
                    var box = DialogBox();
                    if (box == null || !box.IsRunning)
                        return;
                    Check("prologue dialog runs", true);
                    break;
                }

                case 4:
                {
                    // Click through the opening like a player, watching the text for Marao.
                    var box = DialogBox();
                    sawMarao |= DialogMentionsMarao();
                    if (box != null && !box.IsDialogEnded)
                    {
                        if (box.IsWaitingAnswer)
                            box.TrySelectVariant(0);
                        else
                            box.NextAction();
                        clicks++;
                        return;
                    }
                    Check("prologue text is about Marao", sawMarao);
                    Check("opening dialog has several lines", clicks >= 5);
                    Check($"still in '{PrologScene}' after the opening", SceneManager.GetCurrentSceneName() == PrologScene);
                    enteredScenes.Clear();
                    StartHotReload();
                    break;
                }

                case 5:
                {
                    if (!hotReloadDone || Busy || !enteredScenes.Contains(PrologScene))
                        return;
                    var reload = ModManager.Instance.LastReload;
                    Check("in-game hot reload applied", reload?.Applied == true);
                    Check($"hot reload reloaded '{HotReloadedMod}'", reload?.Plan?.Load.Contains(HotReloadedMod) == true);
                    Check("hot reload kept the front-end loaded", reload?.Plan?.Keep.Contains("base_menu") == true);
                    Check("hot reload released every old context", reload?.Leaked.Count == 0);
                    Check($"hot reload kept the game '{MainGame}'", ActiveGame.Id == MainGame);
                    Check($"session came back in '{PrologScene}'", true);
                    GetTree().Root.GetNode<GodotSceneService>("GodotSceneService").SwitchToMenu();
                    break;
                }

                case 6:
                    if (Busy || !PressMenuPlay())
                        return;
                    enteredScenes.Clear();
                    break;

                case 7:
                    if (!StartFromPicker(SecondGame))
                        return;
                    break;

                case 8:
                    if (Busy || ActiveGame.Id != SecondGame || enteredScenes.Count == 0)
                        return;
                    CheckSwitch(SecondGame, expectUnload: true);
                    Check($"entered a scene of '{SecondGame}' ('{enteredScenes[0]}')", true);
                    GetTree().Root.GetNode<GodotSceneService>("GodotSceneService").SwitchToMenu();
                    break;

                case 9:
                    if (Busy || !PressMenuPlay())
                        return;
                    enteredScenes.Clear();
                    break;

                case 10:
                    if (!StartFromPicker(MainGame))
                        return;
                    break;

                case 11:
                    if (Busy || ActiveGame.Id != MainGame || !enteredScenes.Contains(PrologScene))
                        return;
                    CheckSwitch(MainGame, expectUnload: false);
                    Check($"back in '{PrologScene}' after switching games twice", true);
                    Finish();
                    return;
            }

            step++;
        }

        private async void StartHotReload()
        {
            await ModHotReloadService.ReloadNowAsync(new[] { HotReloadedMod });
            hotReloadDone = true;
        }

        /// <summary>The launcher's or the main menu's Play button, pressed; false while neither is on screen.</summary>
        private bool PressMenuPlay()
        {
            if (GetTree().CurrentScene?.FindChild("LauncherPlay", true, false) is Button launcherPlay)
            {
                launcherPlay.EmitSignal(BaseButton.SignalName.Pressed);
                return false;
            }
            if (Menu?.FindChild("play", true, false) is not Button play)
                return false;
            play.EmitSignal(BaseButton.SignalName.Pressed);
            return true;
        }

        /// <summary>Presses New game on the game's card in the picker; false until the picker is open.</summary>
        private bool StartFromPicker(string gameId)
        {
            var picker = Menu?.FindChild("GamePickerPanel", true, false) as Control;
            if (picker?.Visible != true)
                return false;
            var game = FindGame(gameId);
            Check($"game '{gameId}' is listed and ready", game?.IsReady == true);
            var start = StartButton(picker, game?.Name ?? gameId);
            Check($"'{gameId}' card has an enabled New game button", start is { Disabled: false });
            if (start == null || start.Disabled || failures.Count > 0)
            {
                Finish();
                return false;
            }

            start.EmitSignal(BaseButton.SignalName.Pressed);
            Check($"New game of '{gameId}' switches in-process, without a restart", !OS.IsRestartOnExitSet());
            return true;
        }

        private void CheckSwitch(string gameId, bool expectUnload)
        {
            var reload = ModManager.Instance.LastReload;
            Check($"switch into '{gameId}' applied", reload?.Applied == true);
            Check($"switch into '{gameId}' kept the front-end loaded", reload?.Plan?.Keep.Contains("base_menu") == true);
            if (expectUnload)
                Check($"switch into '{gameId}' unloaded mods outside the game", reload?.Plan?.Unload.Count > 0);
            Check($"switch into '{gameId}' released every old context", reload?.Leaked.Count == 0);
            if (reload?.Leaked.Count > 0)
                GameLogger.Log("TEST", $"NewGameTest: leaked contexts: {string.Join(", ", reload.Leaked)}", LogLevel.Error);
        }

        private static ModCollectionGameSummary FindGame(string gameId)
        {
            foreach (var game in GameCatalog.Games())
            {
                if (game.Collection.Id == gameId)
                    return game;
            }
            return null;
        }

        /// <summary>The New game button on the card titled <paramref name="title"/>.</summary>
        private static Button StartButton(Node picker, string title)
        {
            if (picker == null)
                return null;
            var newGameText = TranslationServer.Translate("menu/play/new_game").ToString();
            // A card is the innermost panel holding exactly one New game button (the picker frame holds them all).
            foreach (var card in picker.FindChildren("*", "PanelContainer", true, false))
            {
                Button start = null;
                var starts = 0;
                foreach (var child in card.FindChildren("*", "Button", true, false))
                {
                    if (child is Button button && button.Text == newGameText)
                    {
                        start = button;
                        starts++;
                    }
                }
                if (starts == 1 && HasLabel(card, title))
                    return start;
            }
            return null;
        }

        private static bool HasLabel(Node card, string title)
        {
            foreach (var child in card.FindChildren("*", "Label", true, false))
            {
                if (child is Label label && label.Text == title)
                    return true;
            }
            return false;
        }

        private IDialogBox DialogBox() => GetTree().Root.FindChild("DialogBox", true, false) as IDialogBox;

        private bool DialogMentionsMarao()
        {
            if (DialogBox() is not Node node)
                return false;
            foreach (var child in node.FindChildren("*", "", true, false))
            {
                var text = child switch
                {
                    RichTextLabel r => r.Text,
                    Label l => l.Text,
                    _ => null,
                };
                if (text != null && (text.Contains("Марао") || text.Contains("Marao")))
                    return true;
            }
            return false;
        }

        private void Check(string name, bool ok)
        {
            if (ok)
            {
                passed++;
                GameLogger.Log("TEST", $"NewGameTest: [OK] {name}");
            }
            else
            {
                failures.Add(name);
                GameLogger.Log("TEST", $"NewGameTest: [FAIL] {name}", LogLevel.Error);
            }
        }

        private void Finish()
        {
            if (done)
                return;
            done = true;
            foreach (var failure in failures)
                GameLogger.Log("TEST", $"NewGameTest: failed: {failure}");
            GameLogger.Log("TEST", $"[TEST_RESULT] name=new_game passed={passed} failed={failures.Count}");
            GetTree().Quit(failures.Count == 0 ? 0 : 1);
        }
    }
}
