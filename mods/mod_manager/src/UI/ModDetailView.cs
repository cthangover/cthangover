using System;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Mods.ModManager
{
    /// <summary>
    /// Full-screen page dedicated to one mod (opened by clicking a tile). The
    /// layout lives in <c>scenes/components/mod_detail_view.tscn</c> so it can be
    /// edited visually; this script fills it with data, rebuilds the contextual
    /// action row and reports user intent through events — the manager screen
    /// performs the actual file operations.
    /// <para/>
    /// Conflicting catalog variants (same version, different content) get a red
    /// warning block explaining both sources, on top of the red tile border the
    /// card already shows.
    /// </summary>
    public partial class ModDetailView : VBoxContainer
    {
        private ModCardData _data;
        private bool _nodesReady;

        private TextureRect _coverImage;
        private Button _back;
        private Label _title;
        private Label _author;
        private Label _meta;
        private OptionButton _versions;
        private Label _installed;
        private Label _size;
        private HBoxContainer _actionsRow;
        private VBoxContainer _conflictBox;
        private Label _conflictText;
        private Label _descriptionText;
        private Label _changelogText;
        private VBoxContainer _dependsBox;
        private Label _dependsText;
        private VBoxContainer _usedByBox;
        private Label _usedByText;
        private VBoxContainer _conflictsBox;
        private Label _conflictsText;

        /// <summary>Raised by the back button.</summary>
        public event Action BackRequested;

        /// <summary>Raised when the primary contextual action is pressed.</summary>
        public event Action<ModCardData> ActionPressed;

        /// <summary>Raised when the standalone delete button is pressed.</summary>
        public event Action<ModCardData> DeleteRequested;

        /// <summary>Raised when the enable/disable button is pressed.</summary>
        public event Action<ModCardData> EnabledToggled;

        /// <summary>Raised after a different catalog version is selected.</summary>
        public event Action<ModCardData> VersionChanged;

        /// <summary>Raised when the "in N collections" cross-link is pressed.</summary>
        public event Action CollectionsLinkRequested;

        /// <summary>Displays the given mod; call again with refreshed data after actions.</summary>
        public void Open(ModCardData data)
        {
            _data = data;
            Rebuild();
        }

        private void EnsureNodes()
        {
            if (_nodesReady)
                return;
            _nodesReady = true;

            _coverImage = GetNode<TextureRect>("Scroll/Content/Hero/HeroMargin/HeroRow/Cover/Image");
            _back = GetNode<Button>("Scroll/Content/Hero/HeroMargin/HeroRow/Info/Back");
            _title = GetNode<Label>("Scroll/Content/Hero/HeroMargin/HeroRow/Info/Title");
            _author = GetNode<Label>("Scroll/Content/Hero/HeroMargin/HeroRow/Info/Author");
            _meta = GetNode<Label>("Scroll/Content/Hero/HeroMargin/HeroRow/Info/Meta");
            _versions = GetNode<OptionButton>("Scroll/Content/Hero/HeroMargin/HeroRow/Info/VersionRow/Versions");
            _installed = GetNode<Label>("Scroll/Content/Hero/HeroMargin/HeroRow/Info/VersionRow/Installed");
            _size = GetNode<Label>("Scroll/Content/Hero/HeroMargin/HeroRow/Info/VersionRow/Size");
            _actionsRow = GetNode<HBoxContainer>("Scroll/Content/ActionsRow");
            _conflictBox = GetNode<VBoxContainer>("Scroll/Content/ConflictBox");
            _conflictText = GetNode<Label>("Scroll/Content/ConflictBox/Text");
            _descriptionText = GetNode<Label>("Scroll/Content/DescriptionBox/Text");
            _changelogText = GetNode<Label>("Scroll/Content/ChangelogBox/Text");
            _dependsBox = GetNode<VBoxContainer>("Scroll/Content/DependsBox");
            _dependsText = GetNode<Label>("Scroll/Content/DependsBox/Text");
            _usedByBox = GetNode<VBoxContainer>("Scroll/Content/UsedByBox");
            _usedByText = GetNode<Label>("Scroll/Content/UsedByBox/Text");
            _conflictsBox = GetNode<VBoxContainer>("Scroll/Content/ConflictsBox");
            _conflictsText = GetNode<Label>("Scroll/Content/ConflictsBox/Text");

            _back.Text = TranslationServer.Translate("mod_manager/back_to_list");
            _back.Pressed += OnBackPressed;
            _versions.ItemSelected += OnVersionSelected;

            _author.AddThemeColorOverride("font_color", UiTokens.Muted);
            _installed.AddThemeColorOverride("font_color", UiTokens.Muted);
            _size.AddThemeColorOverride("font_color", UiTokens.Muted);
            _conflictText.AddThemeColorOverride("font_color", UiTokens.Danger);

            SetCaption("Scroll/Content/ConflictBox/Caption", "mod_manager/conflict_title");
            SetCaption("Scroll/Content/DescriptionBox/Caption", "mod_manager/description");
            SetCaption("Scroll/Content/ChangelogBox/Caption", "mod_manager/changelog");
            SetCaption("Scroll/Content/DependsBox/Caption", "mod_manager/depends");
            SetCaption("Scroll/Content/UsedByBox/Caption", "mod_manager/used_by");
            SetCaption("Scroll/Content/ConflictsBox/Caption", "mod_manager/conflicts");
            SetCaption("Scroll/Content/Hero/HeroMargin/HeroRow/Info/VersionRow/Caption", "mod_manager/version");
        }

        private void SetCaption(string path, string localeKey)
        {
            var caption = GetNode<Label>(path);
            caption.Text = TranslationServer.Translate(localeKey);
            caption.AddThemeColorOverride("font_color", UiTokens.Muted);
        }

        private void Rebuild()
        {
            EnsureNodes();
            if (_data == null)
                return;

            _title.Text = _data.Title ?? _data.Id;
            _author.Text = _data.Author ?? string.Empty;

            if (_data.IsConflict)
            {
                _meta.Text = TranslationServer.Translate("mod_manager/status_conflict");
                _meta.AddThemeColorOverride("font_color", UiTokens.Danger);
            }
            else
            {
                _meta.Text = $"{ModCategoryNames.Translate(ModCategoryNames.Primary(_data.Category))}  ·  {ModStatusNames.Translate(_data.Status)}";
                _meta.AddThemeColorOverride("font_color", ModStatusNames.Color(_data.Status));
            }

            PopulateVersions();
            _installed.Text = _data.IsInstalled
                ? string.Format(TranslationServer.Translate("mod_manager/installed_version"), _data.LocalVersion ?? "—")
                : TranslationServer.Translate("mod_manager/not_installed_hint").ToString();

            var size = _data.SelectedDownload?.SizeBytes ?? 0;
            _size.Text = size > 0 ? FormatSize(size) : string.Empty;

            BuildActions();
            BuildConflictBox();

            _descriptionText.Text = NullIfEmpty(_data.Description) ?? "—";
            _changelogText.Text = NullIfEmpty(_data.SelectedDownload?.Changelog) ?? "—";
            SetOptional(_dependsBox, _dependsText, JoinOrEmpty(ModQueries.GetDependencies(_data)));
            SetOptional(_usedByBox, _usedByText, JoinOrEmpty(ModQueries.GetDependents(_data.Id)));
            SetOptional(_conflictsBox, _conflictsText, JoinOrEmpty(ModQueries.GetConflicts(_data)));

            LoadCover();
        }

        private void PopulateVersions()
        {
            _versions.Clear();
            _versions.Visible = _data.Downloads.Count > 0;
            if (_data.Downloads.Count == 0)
                return;

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

        private void BuildActions()
        {
            foreach (var child in _actionsRow.GetChildren())
            {
                _actionsRow.RemoveChild(child);
                child.QueueFree();
            }

            var primary = ModActions.GetPrimary(_data);
            if (primary != null)
            {
                var button = new Button { Text = ModActionNames.Translate(primary.Value) };
                if (ModActionNames.IsDanger(primary.Value))
                    CthangoverTheme.ApplyDanger(button);
                else
                    CthangoverTheme.ApplyPrimary(button);
                button.Pressed += OnPrimaryPressed;
                _actionsRow.AddChild(button);
            }

            if (primary != ModActionKind.Delete && _data.IsDeletable)
            {
                var delete = new Button { Text = ModActionNames.Translate(ModActionKind.Delete) };
                CthangoverTheme.ApplyDanger(delete);
                delete.Pressed += OnDeletePressed;
                _actionsRow.AddChild(delete);
            }

            if (_data.CanToggleEnabled)
            {
                var toggle = new Button
                {
                    Text = ModActionNames.Translate(_data.IsDisabled
                        ? ModActionKind.Enable
                        : ModActionKind.Disable),
                };
                if (_data.IsDisabled)
                    CthangoverTheme.ApplyPrimary(toggle);
                else
                    CthangoverTheme.ApplyDanger(toggle);
                toggle.Pressed += OnTogglePressed;
                _actionsRow.AddChild(toggle);
            }

            if (_data.IsProtected)
            {
                var hint = new Label
                {
                    Text = TranslationServer.Translate("mod_manager/self_hint"),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                hint.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);
                hint.AddThemeColorOverride("font_color", UiTokens.Muted);
                _actionsRow.AddChild(hint);
            }

            var collectionsText = ModCollectionLinks.BadgeText(_data.CollectionsCount);
            if (collectionsText.Length > 0)
            {
                var link = new Button
                {
                    Text = collectionsText,
                    TooltipText = collectionsText,
                    FocusMode = FocusModeEnum.None,
                };
                link.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);
                link.AddThemeColorOverride("font_color", UiTokens.TypeTarget);
                link.AddThemeColorOverride("font_hover_color", UiTokens.TypeTarget);
                link.Pressed += () => CollectionsLinkRequested?.Invoke();
                _actionsRow.AddChild(link);
            }
        }

        private void BuildConflictBox()
        {
            _conflictBox.Visible = _data.IsConflict;
            if (!_data.IsConflict)
            {
                _conflictText.Text = string.Empty;
                return;
            }

            var sources = ModCatalogSource.Tooltip(_data.SourceUrls);
            _conflictText.Text = sources == null
                ? TranslationServer.Translate("mod_manager/conflict_warning")
                : $"{TranslationServer.Translate("mod_manager/conflict_warning")}\n{sources}";
        }

        private void LoadCover()
        {
            _coverImage.Texture = null;

            if (_data.LocalInfo != null)
            {
                var local = ModPreviewLoader.LoadLocal(_data.LocalInfo);
                if (local != null)
                {
                    _coverImage.Texture = local;
                    return;
                }
            }

            if (_data.Catalog != null)
                ModPreviewLoader.LoadRemote(_data.Id, _data.Catalog.IconUrl, OnRemotePreviewLoaded);
        }

        private void OnRemotePreviewLoaded(Texture2D texture)
        {
            if (texture != null && GodotObject.IsInstanceValid(_coverImage))
                _coverImage.Texture = texture;
        }

        private void OnBackPressed() => BackRequested?.Invoke();

        private void OnPrimaryPressed() => ActionPressed?.Invoke(_data);

        private void OnDeletePressed() => DeleteRequested?.Invoke(_data);

        private void OnTogglePressed() => EnabledToggled?.Invoke(_data);

        private void OnVersionSelected(long index)
        {
            if (index < 0 || index >= _data.Downloads.Count)
                return;

            _data.SelectedDownload = _data.Downloads[(int)index];
            _data.RecomputeStatus();
            Rebuild();
            VersionChanged?.Invoke(_data);
        }

        private static void SetOptional(VBoxContainer box, Label text, string value)
        {
            box.Visible = !string.IsNullOrEmpty(value);
            text.Text = value ?? string.Empty;
        }

        private static string NullIfEmpty(string value)
            => string.IsNullOrWhiteSpace(value) ? null : value;

        private static string JoinOrEmpty(System.Collections.Generic.List<string> values)
            => values == null || values.Count == 0 ? null : string.Join(", ", values);

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1024L * 1024L)
                return $"{bytes / (1024f * 1024f):F1} MB";
            if (bytes >= 1024L)
                return $"{bytes / 1024f:F0} KB";
            return $"{bytes} B";
        }
    }
}
