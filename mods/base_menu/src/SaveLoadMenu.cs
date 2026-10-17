using System;
using System.Collections.Generic;
using Cthangover.Core.Games;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Menu;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.BaseMenu
{
    /// <summary>
    /// Save/load browser with unlimited slots. The grid shows 4×3 slots per
    /// screen and scrolls past that. Saves belong to games: in a game the
    /// panel shows that game's slots (save mode appends one empty "new"
    /// slot); from the front-end the selector at the top switches between
    /// games with saves and the saves made before games existed. Loading goes
    /// through <see cref="GameFlow.RequestLoad"/> — composition comparison,
    /// backup, restart into another game, adoption of game-less saves.
    /// </summary>
    public partial class SaveLoadMenu : Control, ISaveLoadPanel
    {
        private Label _titleLabel;
        private OptionButton _gameSelect;
        private SaveSlotList _slotList;
        private Label _emptyLabel;
        private Button _backBtn;

        private bool _isLoadMode;
        private string _gameId;
        private readonly List<string> _gameIds = new();
        private List<SaveSlotInfo> _currentSlots;

        /// <summary>
        /// Raised when the player clicks the back button to dismiss the save/load screen.
        /// </summary>
        public event Action Closed;

        public override void _Ready()
        {
            _titleLabel = GetNode<Label>("Panel/Margin/VBox/Title");
            _gameSelect = GetNode<OptionButton>("Panel/Margin/VBox/GameSelect");
            _slotList = GetNode<SaveSlotList>("Panel/Margin/VBox/Scroll/SlotGrid");
            _emptyLabel = GetNode<Label>("Panel/Margin/VBox/EmptyLabel");
            _backBtn = GetNode<Button>("Panel/Margin/VBox/BackBtn");
            CthangoverTheme.ApplyIcon(_backBtn);

            _slotList.SlotPressed += OnSlotPressed;
            _gameSelect.ItemSelected += OnGameSelected;
            _backBtn.Pressed += OnBackPressed;
            _backBtn.Text = TranslationServer.Translate("settings/back");
            Visible = false;
        }

        /// <summary>Load mode: the running game's saves, or every game's from the front-end.</summary>
        public void OpenForLoad() => OpenFor(true, ActiveGame.Id);

        /// <summary>Save mode: always the running game's slots plus a free one.</summary>
        public void OpenForSave() => OpenFor(false, ActiveGame.Id);

        /// <summary>Load mode preselected on <paramref name="gameId"/> (game picker → "Saves").</summary>
        public void OpenForGame(string gameId) => OpenFor(true, gameId);

        /// <inheritdoc cref="IMenuPanel" />
        public void Open() => OpenForLoad();

        private void OpenFor(bool load, string gameId)
        {
            _isLoadMode = load;
            _titleLabel.Text = TranslationServer.Translate(load ? "ui/load/title" : "ui/save/title");
            BuildGameSelect(gameId);
            Visible = true;
            CallDeferred(nameof(RefreshSlots));
        }

        /// <summary>
        /// Games offered by the selector: in save mode only the running game;
        /// in load mode every game with saves (the running/preselected one first)
        /// and, last, the saves made without a game.
        /// </summary>
        private void BuildGameSelect(string preselected)
        {
            _gameIds.Clear();
            _gameSelect.Clear();

            if (!_isLoadMode)
            {
                AddGame(ActiveGame.Id, ActiveGame.Collection?.DisplayTitle);
            }
            else
            {
                if (!string.IsNullOrEmpty(preselected))
                    AddGame(preselected, GameName(preselected));
                foreach (var game in SaveService.ListGamesWithSaves())
                {
                    if (!_gameIds.Contains(game.GameId))
                        AddGame(game.GameId, game.GameId == null ? null : GameName(game.GameId) ?? game.GameName);
                }
                if (_gameIds.Count == 0)
                    AddGame(preselected, GameName(preselected));
            }

            _gameId = _gameIds.Count > 0 ? _gameIds[0] : null;
            _gameSelect.Selected = _gameIds.Count > 0 ? 0 : -1;
            _gameSelect.Visible = _gameIds.Count > 1 || (_gameIds.Count == 1 && _gameIds[0] == null && ActiveGame.IsActive);
        }

        private void AddGame(string gameId, string name)
        {
            _gameIds.Add(gameId);
            _gameSelect.AddItem(gameId == null
                ? TranslationServer.Translate("menu/load/legacy_group")
                : name ?? gameId);
        }

        private static string GameName(string gameId)
        {
            if (string.IsNullOrEmpty(gameId))
                return null;
            if (ActiveGame.Id == gameId)
                return ActiveGame.Collection?.GetLocalizedName(TranslationServer.GetLocale());
            return GameCatalog.LoadCollections().TryGetValue(gameId, out var collection)
                ? CollectionText.Resolve(collection.GetLocalizedName(TranslationServer.GetLocale()))
                : null;
        }

        private void OnGameSelected(long index)
        {
            _gameId = index >= 0 && index < _gameIds.Count ? _gameIds[(int)index] : null;
            RefreshSlots();
        }

        /// <summary>Lists the selected game's saves; save mode appends the next free slot.</summary>
        public void RefreshSlots()
        {
            _currentSlots = SaveService.ListSaves(_gameId);
            if (!_isLoadMode)
                _currentSlots.Add(SaveService.EmptySlot(_gameId, SaveService.NextSlotName(_gameId)));

            _emptyLabel.Visible = _currentSlots.Count == 0;
            _emptyLabel.Text = TranslationServer.Translate("menu/load/empty");
            _slotList.SetSlots(_currentSlots);
        }

        private void OnSlotPressed(string fileName)
        {
            var slot = FindSlot(fileName);

            if (_isLoadMode)
            {
                if (slot == null || slot.IsEmpty)
                    return;

                GameFlow.RequestLoad(this, _gameId, fileName);
                return;
            }

            if (slot == null || slot.IsEmpty)
            {
                SaveScreenshotService.CaptureAndSave(fileName);
                SaveService.Save(fileName);
                RefreshSlots();
            }
            else
            {
                ShowOverwriteConfirm(fileName);
            }
        }

        private SaveSlotInfo FindSlot(string fileName)
            => _currentSlots?.Find(s => s.FileName == fileName);

        private void ShowOverwriteConfirm(string fileName)
        {
            var dialog = new ConfirmationDialog();
            dialog.Title = TranslationServer.Translate("ui/save/overwrite_title");
            dialog.DialogText = TranslationServer.Translate("ui/save/overwrite_text");
            dialog.OkButtonText = TranslationServer.Translate("ui/save/save_button");
            dialog.CancelButtonText = TranslationServer.Translate("ui/save/cancel_button");
            dialog.Exclusive = true;
            AddChild(dialog);
            CthangoverTheme.ApplyPrimary(dialog.GetOkButton());

            dialog.Confirmed += () =>
            {
                SaveScreenshotService.CaptureAndSave(fileName);
                SaveService.Save(fileName);
                RefreshSlots();
            };

            dialog.PopupCentered();
        }

        private void OnBackPressed()
        {
            Visible = false;
            Closed?.Invoke();
        }
    }
}
