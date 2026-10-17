using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cthangover.Core.Games;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.BaseMenu
{
    /// <summary>
    /// Menu-side rules for entering games: "Continue", starting a game and
    /// loading a save. A game other than the running one (or any game from
    /// the front-end) is entered by replacing the mod set in-process
    /// (<see cref="GameSession.SwitchToAsync"/>). Before a save is loaded into a changed composition
    /// the player sees what changes (added mechanics start fresh, removed ones
    /// keep their data dormant) and a backup is taken.
    /// </summary>
    internal static class GameFlow
    {
        /// <summary>Set after installing mods in this process: entering any game, even the running one, reloads the mod set to pick them up.</summary>
        public static bool ReloadRequired { get; set; }

        public static bool HasContinue() => LastSessionStore.Load() != null;

        public static void Continue(Node host)
        {
            var last = LastSessionStore.Load();
            if (last != null)
                RequestLoad(host, last.GameId, last.FileName);
        }

        public static void StartGame(Node host, string gameId)
        {
            if (IsRunning(gameId) && !ReloadRequired)
                GameSession.StartNew(host);
            else
                Switch(host, gameId, null);
        }

        /// <summary>
        /// Loads <paramref name="fileName"/> of <paramref name="gameId"/> (null =
        /// a save made without a game), comparing compositions first.
        /// </summary>
        public static void RequestLoad(Node host, string gameId, string fileName)
        {
            if (string.IsNullOrEmpty(gameId))
            {
                // Development launch with every mod: the save belongs to that
                // mod set, load it as before.
                if (!ActiveGame.IsActive && !ActiveGame.IsFrontendOnly)
                {
                    GameSession.LoadAndEnter(host, fileName);
                    return;
                }

                ChooseGameForSave(host, fileName);
                return;
            }

            var package = SaveService.ReadPackage(gameId, fileName);
            var diff = SaveCompositionDiff.Compute(package?.Game?.Mods, GameCatalog.Composition(gameId));
            if (diff.IsEmpty)
            {
                Proceed(host, gameId, fileName, backup: false);
                return;
            }

            Confirm(host,
                TranslationServer.Translate("menu/load/diff_title"),
                DescribeDiff(diff),
                TranslationServer.Translate("menu/load/diff_ok"),
                () => Proceed(host, gameId, fileName, backup: true));
        }

        private static void Proceed(Node host, string gameId, string fileName, bool backup)
        {
            if (backup)
                SaveService.BackupSave(gameId, fileName);

            if (IsRunning(gameId) && !ReloadRequired)
                GameSession.LoadAndEnter(host, fileName);
            else
                Switch(host, gameId, fileName);
        }

        private static async void Switch(Node host, string gameId, string fileName)
        {
            if (await GameSession.SwitchToAsync(host, gameId, fileName))
                ReloadRequired = false;
        }

        private static bool IsRunning(string gameId)
            => ActiveGame.IsActive && string.Equals(ActiveGame.Id, gameId, StringComparison.OrdinalIgnoreCase);

        /// <summary>Lists what a composition change does to the save, in player terms.</summary>
        public static string DescribeDiff(SaveCompositionDiff diff)
        {
            var text = new StringBuilder(TranslationServer.Translate("menu/load/diff_intro"));
            void Section(string key, IEnumerable<string> lines)
            {
                var list = lines.ToList();
                if (list.Count == 0)
                    return;
                text.Append("\n\n").Append(TranslationServer.Translate(key));
                foreach (var line in list)
                    text.Append("\n  • ").Append(line);
            }

            Section("menu/load/diff_added", diff.Added.Select(ModTitle));
            Section("menu/load/diff_removed", diff.Removed.Select(ModTitle));
            Section("menu/load/diff_updated", diff.Updated.Select(u =>
                $"{ModTitle(u.ModId)}: {u.From} → {u.To}" + (u.Downgrade ? " " + TranslationServer.Translate("menu/load/diff_downgrade") : string.Empty)));
            text.Append("\n\n").Append(TranslationServer.Translate("menu/load/diff_backup"));
            return text.ToString();
        }

        private static string ModTitle(string modId)
            => ModRegistry.Instance.AllDiscoveredMods.TryGetValue(modId, out var info) && !string.IsNullOrEmpty(info.DisplayTitle)
                ? $"{info.DisplayTitle} ({modId})"
                : modId;

        /// <summary>A save made before games existed: the player picks the game it belongs to; it moves there.</summary>
        private static void ChooseGameForSave(Node host, string fileName)
        {
            var games = GameCatalog.Games().Where(g => g.IsReady).ToList();
            if (games.Count == 0)
            {
                Confirm(host, TranslationServer.Translate("menu/load/legacy_title"),
                    TranslationServer.Translate("menu/load/legacy_no_games"), null, null);
                return;
            }

            var select = new OptionButton();
            foreach (var game in games)
                select.AddItem(game.Name ?? game.Collection.Id);

            var content = new VBoxContainer();
            content.AddChild(new Label
            {
                Text = TranslationServer.Translate("menu/load/legacy_text"),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(420, 0),
            });
            content.AddChild(select);

            var dialog = new ConfirmationDialog
            {
                Title = TranslationServer.Translate("menu/load/legacy_title"),
                OkButtonText = TranslationServer.Translate("menu/load/legacy_ok"),
                CancelButtonText = TranslationServer.Translate("ui/save/cancel_button"),
                Exclusive = true,
            };
            dialog.AddChild(content);
            dialog.Confirmed += () =>
            {
                var gameId = games[Math.Max(0, select.Selected)].Collection.Id;
                dialog.QueueFree();
                var slot = SaveService.AdoptLegacySave(fileName, gameId);
                RequestLoad(host, gameId, slot);
            };
            dialog.Canceled += dialog.QueueFree;
            host.AddChild(dialog);
            dialog.PopupCentered();
        }

        /// <summary>Message or confirmation dialog; without <paramref name="onConfirm"/> it is a plain notice.</summary>
        public static void Confirm(Node host, string title, string text, string okText, Action onConfirm)
        {
            AcceptDialog dialog = onConfirm == null
                ? new AcceptDialog()
                : new ConfirmationDialog { CancelButtonText = TranslationServer.Translate("ui/save/cancel_button") };
            dialog.Title = title;
            dialog.DialogText = text;
            dialog.Exclusive = true;
            if (!string.IsNullOrEmpty(okText))
                dialog.OkButtonText = okText;
            dialog.Confirmed += () =>
            {
                dialog.QueueFree();
                onConfirm?.Invoke();
            };
            dialog.Canceled += dialog.QueueFree;
            host.AddChild(dialog);
            if (onConfirm != null)
                CthangoverTheme.ApplyPrimary(dialog.GetOkButton());
            dialog.PopupCentered();
        }
    }
}
