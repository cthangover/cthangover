using System;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Mods.ModManager
{
    /// <summary>
    /// Nexus-style mod tile. The layout lives in
    /// <c>scenes/components/mod_card.tscn</c> so it can be edited in the Godot
    /// editor; this script only binds data and reacts to input. It exposes a
    /// cover image, title (always two lines tall), author, catalog source, status
    /// badge, version combo box, a single contextual action button and an
    /// enable/disable switch.
    /// <para/>
    /// All rows are reserved to the same height on purpose: tiles in the grid must
    /// look identical regardless of whether a mod has a catalog entry, versions,
    /// an author or an action. Version selection mutates the shared
    /// <see cref="ModCardData.SelectedDownload"/> and refreshes the tile in place,
    /// so changing versions never rebuilds the grid or loses scroll position.
    /// </summary>
    public partial class ModCard : PanelContainer
    {
        private ModCardData _data;
        private TextureRect _image;
        private Label _title;
        private Label _author;
        private Label _source;
        private Label _status;
        private OptionButton _versions;
        private Button _action;
        private CheckButton _enabled;
        private HBoxContainer _metaRow;
        private Button _collectionsLink;
        private bool _updatingEnabled;
        private bool _initialized;

        /// <summary>Raised when the card body (not a control) is clicked.</summary>
        public event Action<ModCardData> Clicked;

        /// <summary>Raised when the "in N collections" cross-link badge is pressed.</summary>
        public event Action CollectionsLinkRequested;

        /// <summary>Raised when the contextual action button is pressed.</summary>
        public event Action<ModCardData> ActionPressed;

        /// <summary>Raised when the enable switch is flipped; the status is already updated.</summary>
        public event Action<ModCardData> EnabledToggled;

        /// <summary>Raised after a different catalog version was selected.</summary>
        public event Action<ModCardData> VersionChanged;

        /// <summary>
        /// Binds the tile to its data. Called once by <see cref="ModGridView"/>
        /// right after the scene is instantiated; the card cannot be used before.
        /// </summary>
        public void Init(ModCardData data)
        {
            if (_initialized)
                return;
            _initialized = true;
            _data = data;

            _image = GetNode<TextureRect>("VBox/Cover/Image");
            _title = GetNode<Label>("VBox/Body/Box/Title");
            _author = GetNode<Label>("VBox/Body/Box/Author");
            _source = GetNode<Label>("VBox/Body/Box/Source");
            _status = GetNode<Label>("VBox/Body/Box/MetaRow/Status");
            _versions = GetNode<OptionButton>("VBox/Body/Box/MetaRow/VersionHost/Versions");
            _action = GetNode<Button>("VBox/Body/Box/ActionRow/ActionHost/Action");
            _enabled = GetNode<CheckButton>("VBox/Body/Box/ActionRow/EnabledHost/Enabled");
            _metaRow = GetNode<HBoxContainer>("VBox/Body/Box/MetaRow");

            _author.AddThemeColorOverride("font_color", UiTokens.Muted);
            _source.AddThemeColorOverride("font_color", UiTokens.Muted);
            AddThemeStyleboxOverride("panel", ModManagerTheme.CreateCardStyle(data.IsConflict));

            _versions.ItemSelected += OnVersionSelected;
            _action.Pressed += OnActionPressed;
            _enabled.Toggled += OnEnabledPressed;
            GuiInput += OnGuiInput;

            BuildCollectionsLink();
            PopulateVersions();
            RefreshState();
            LoadPreview();
        }

        /// <summary>Resynchronizes badge, version list, action and switch with the data.</summary>
        public void RefreshState()
        {
            if (!_initialized)
                return;

            _title.Text = _data.Title ?? _data.Id;
            _author.Text = _data.Author ?? string.Empty;
            UpdateSourceLabel();
            UpdateCollectionsLink();

            if (_data.IsConflict)
            {
                _status.Text = TranslationServer.Translate("mod_manager/status_conflict");
                _status.AddThemeColorOverride("font_color", UiTokens.Danger);
                _status.TooltipText = TranslationServer.Translate("mod_manager/conflict_warning");
            }
            else
            {
                _status.Text = ModStatusNames.Translate(_data.Status);
                _status.AddThemeColorOverride("font_color", ModStatusNames.Color(_data.Status));
                _status.TooltipText = string.Empty;
            }

            UpdateActionButton();
            UpdateEnabledToggle();
        }

        private void UpdateSourceLabel()
        {
            if (_data.Catalog == null)
            {
                _source.Text = TranslationServer.Translate("mod_manager/source_local");
                _source.TooltipText = string.Empty;
                return;
            }

            var label = ModCatalogSource.Label(_data.SourceUrls) ?? "—";
            _source.Text = string.Format(TranslationServer.Translate("mod_manager/source_catalog"), label);
            _source.TooltipText = ModCatalogSource.Tooltip(_data.SourceUrls) ?? string.Empty;
        }

        private void PopulateVersions()
        {
            _versions.Clear();

            if (_data.Downloads.Count == 0)
            {
                _versions.AddItem("v" + (_data.LocalVersion ?? "—"));
                _versions.Disabled = true;
                return;
            }

            _versions.Disabled = false;
            var selectedIndex = 0;
            for (var i = 0; i < _data.Downloads.Count; i++)
            {
                var download = _data.Downloads[i];
                var label = "v" + (download.Version ?? "?");
                if (i == 0)
                    label += " · " + TranslationServer.Translate("mod_manager/latest");
                if (_data.IsInstalled
                    && !string.IsNullOrEmpty(download.Version)
                    && string.Equals(download.Version, _data.LocalVersion, StringComparison.OrdinalIgnoreCase))
                {
                    label += " · " + TranslationServer.Translate("mod_manager/installed_marker");
                }

                _versions.AddItem(label);

                if (ReferenceEquals(download, _data.SelectedDownload))
                    selectedIndex = i;
            }

            _versions.Selected = selectedIndex;
        }

        /// <summary>
        /// Builds the flat "in N collections" cross-link shown between the
        /// status label and the version combo. The link is part of the hub
        /// mockup: pressing it jumps to the collections tab. Hidden entirely
        /// for mods that no playable game references.
        /// </summary>
        private void BuildCollectionsLink()
        {
            _collectionsLink = new Button
            {
                FocusMode = FocusModeEnum.None,
                Visible = false,
            };
            _collectionsLink.AddThemeFontSizeOverride("font_size", UiTokens.FontTiny);
            _collectionsLink.AddThemeColorOverride("font_color", UiTokens.TypeTarget);
            _collectionsLink.AddThemeColorOverride("font_hover_color", UiTokens.TypeTarget);
            _collectionsLink.AddThemeColorOverride("font_pressed_color", UiTokens.TypeTarget);
            _collectionsLink.AddThemeColorOverride("font_focus_color", UiTokens.TypeTarget);
            foreach (var state in new[] { "normal", "hover", "pressed", "disabled", "focus" })
                _collectionsLink.AddThemeStyleboxOverride(state, new StyleBoxEmpty());
            _collectionsLink.Pressed += () => CollectionsLinkRequested?.Invoke();
            _metaRow.AddChild(_collectionsLink);
            _metaRow.MoveChild(_collectionsLink, 1);
        }

        private void UpdateCollectionsLink()
        {
            var text = ModCollectionLinks.BadgeText(_data.CollectionsCount);
            _collectionsLink.Visible = text.Length > 0;
            _collectionsLink.Text = text;
            _collectionsLink.TooltipText = text;
        }

        private void UpdateActionButton()
        {
            var kind = ModActions.GetPrimary(_data);
            if (kind == null)
            {
                _action.Visible = false;
                return;
            }

            _action.Visible = true;
            _action.Text = ModActionNames.Translate(kind.Value);

            if (ModActionNames.IsDanger(kind.Value))
                CthangoverTheme.ApplyDanger(_action);
            else
                CthangoverTheme.ApplyPrimary(_action);
        }

        private void UpdateEnabledToggle()
        {
            _enabled.Visible = _data.CanToggleEnabled;
            if (!_data.CanToggleEnabled)
                return;

            _updatingEnabled = true;
            _enabled.ButtonPressed = !_data.IsDisabled;
            _updatingEnabled = false;
            _enabled.TooltipText = TranslationServer.Translate(_data.IsDisabled
                ? "mod_manager/action_enable"
                : "mod_manager/action_disable");
        }

        private void LoadPreview()
        {
            if (_data.LocalInfo != null)
            {
                var local = ModPreviewLoader.LoadLocal(_data.LocalInfo);
                if (local != null)
                {
                    _image.Texture = local;
                    return;
                }
            }

            if (_data.Catalog != null)
                ModPreviewLoader.LoadRemote(_data.Id, _data.Catalog.IconUrl, OnRemotePreviewLoaded);
        }

        private void OnRemotePreviewLoaded(Texture2D texture)
        {
            if (texture != null && GodotObject.IsInstanceValid(_image))
                _image.Texture = texture;
        }

        private void OnGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mouseButton
                && mouseButton.Pressed
                && mouseButton.ButtonIndex == MouseButton.Left)
            {
                Clicked?.Invoke(_data);
                AcceptEvent();
            }
        }

        private void OnActionPressed()
        {
            ActionPressed?.Invoke(_data);
        }

        private void OnEnabledPressed(bool pressed)
        {
            if (_updatingEnabled || !_data.CanToggleEnabled)
                return;

            _data.IsDisabled = !pressed;
            _data.RecomputeStatus();
            RefreshState();
            EnabledToggled?.Invoke(_data);
        }

        private void OnVersionSelected(long index)
        {
            if (index < 0 || index >= _data.Downloads.Count)
                return;

            _data.SelectedDownload = _data.Downloads[(int)index];
            _data.RecomputeStatus();
            RefreshState();
            VersionChanged?.Invoke(_data);
        }
    }
}
