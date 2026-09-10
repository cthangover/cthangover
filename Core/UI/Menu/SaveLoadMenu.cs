using System.Collections.Generic;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Godot;

namespace Cthangover.Core.UI.Menu
{
    /// <summary>
    /// Save/load slot browser. Operates in dual mode (save vs. load) set by
    /// OpenForSave/OpenForLoad. In load mode, clicking an occupied slot loads
    /// the game and transitions to BaseScene with the saved scene name as
    /// pending. In save mode, clicking an empty slot captures a screenshot
    /// then saves; clicking an occupied slot shows an overwrite confirmation
    /// dialog. RefreshSlots is called deferred to ensure the UI is visible
    /// before populating (avoids layout glitches from invisible children).
    /// </summary>
    public partial class SaveLoadMenu : Control
    {
        private Label _titleLabel;
        private SaveSlotList _slotList;
        private Button _backBtn;

        private bool _isLoadMode;
        private const int SlotCount = 12;
        private List<SaveSlotInfo> _currentSlots;

        /// <summary>
        /// Emitted when the player clicks the back button to dismiss the save/load screen.
        /// </summary>
        [Signal]
        public delegate void ClosedEventHandler();

        public override void _Ready()
        {
            _titleLabel = GetNode<Label>("Panel/Margin/VBox/Title");
            _slotList = GetNode<SaveSlotList>("Panel/Margin/VBox/SlotGrid");
            _backBtn = GetNode<Button>("Panel/Margin/VBox/BackBtn");

            _slotList.SlotPressed += OnSlotPressed;
            _backBtn.Pressed += OnBackPressed;
            _backBtn.Text = TranslationServer.Translate("settings/back");
            Visible = false;
        }

        /// <summary>
        /// Opens the menu in load mode. Sets the title to the localized load label, shows the
        /// panel, and defers <see cref="RefreshSlots"/> so the control gets its final size
        /// before the grid layout calculates cell dimensions.
        /// </summary>
        public void OpenForLoad()
        {
            _isLoadMode = true;
            _titleLabel.Text = TranslationServer.Translate("ui/load/title");
            Visible = true;
            CallDeferred(nameof(RefreshSlots));
        }

        /// <summary>
        /// Opens the menu in save mode. Same deferred refresh strategy as <see cref="OpenForLoad"/>.
        /// </summary>
        public void OpenForSave()
        {
            _isLoadMode = false;
            _titleLabel.Text = TranslationServer.Translate("ui/save/title");
            Visible = true;
            CallDeferred(nameof(RefreshSlots));
        }

        /// <summary>
        /// Queries <see cref="SaveService.GetSaveSlots"/> for the configured number of slots
        /// and passes the results to the <see cref="SaveSlotList"/> grid for rendering.
        /// Called deferred from <see cref="OpenForLoad"/> and <see cref="OpenForSave"/> to
        /// ensure the UI has received its final layout size.
        /// </summary>
        public void RefreshSlots()
        {
            _currentSlots = SaveService.GetSaveSlots(SlotCount);
            _slotList.SetSlots(_currentSlots);
        }

        private void OnSlotPressed(string fileName)
        {
            var slot = FindSlot(fileName);

            if (_isLoadMode)
            {
                if (slot == null || slot.IsEmpty)
                    return;

                if (SaveService.Load(fileName))
                {
                    var sceneManager = SceneContextNode.FindNode<SceneManager>("SceneManager");
                    if (sceneManager != null)
                    {
                        sceneManager.Initialize();

                        var loadedSceneName = slot.SceneName;
                        if (!string.IsNullOrEmpty(loadedSceneName) && loadedSceneName != "unknown")
                        {
                            sceneManager.PendingSceneName = loadedSceneName;
                        }

                        var sceneService = SceneContextNode.FindNode<GodotSceneService>("GodotSceneService");
                        sceneService?.LoadScene("res://scenes/ui/base_scene.tscn");
                    }
                }
            }
            else
            {
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
        }

        private SaveSlotInfo FindSlot(string fileName)
        {
            if (_currentSlots == null)
                return null;

            foreach (var s in _currentSlots)
            {
                if (s.FileName == fileName)
                    return s;
            }
            return null;
        }

        private void ShowOverwriteConfirm(string fileName)
        {
            var dialog = new ConfirmationDialog();
            dialog.Title = TranslationServer.Translate("ui/save/overwrite_title");
            dialog.DialogText = TranslationServer.Translate("ui/save/overwrite_text");
            dialog.OkButtonText = TranslationServer.Translate("ui/save/save_button");
            dialog.CancelButtonText = TranslationServer.Translate("ui/save/cancel_button");
            dialog.Exclusive = true;
            AddChild(dialog);

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
            EmitSignal(SignalName.Closed);
        }
    }
}
