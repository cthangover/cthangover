using System;
using Cthangover.Core.Mods;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.ModManager
{
    /// <summary>
    /// Root of the mod manager screen. Opened from the main menu through the
    /// manifest <c>main_menu</c> button as a fullscreen overlay; closing frees the
    /// scene. The visual layout is decomposed into editable scenes
    /// (<c>mod_filter_panel</c>, <c>mod_grid_view</c>/<c>mod_grid_group</c>,
    /// <c>mod_card</c>, <c>mod_detail_view</c>) referenced from
    /// <c>scenes/mod_manager_screen.tscn</c>; this class only wires them to the
    /// model, runs mod operations (install/update/delete/enable) and reflects that
    /// all of them only take effect after a restart.
    /// </summary>
    public partial class ModManagerScreen : ModWidget
    {
        private ModManagerModel _model;
        private ModFilterState _filter;
        private ModGridView _grid;
        private ModDetailView _detail;
        private ModFilterPanel _filterPanel;
        private LineEdit _search;
        private OptionButton _sort;
        private Label _resultLabel;
        private PanelContainer _bannerPanel;
        private Label _bannerLabel;
        private Button _refreshButton;

        private string _installingId;
        private bool _restartRequired;
        private string _statusMessage;

        protected override void Construct()
        {
            MouseFilter = MouseFilterEnum.Stop;
            CthangoverTheme.Apply(this);
            _model = new ModManagerModel();
            _filter = new ModFilterState();

            BindSceneNodes();

            SubscribeEvents();
            CatalogService.LoadCache();
            _model.Reload();
            _filterPanel.Bind(_filter);
            RefreshAll();
            StartCatalogRefreshIfNeeded();
        }

        protected override void Destruct()
        {
            UnsubscribeEvents();
        }

        /// <summary>Fetches the scene-built nodes, sets static captions and hooks the layout controls.</summary>
        private void BindSceneNodes()
        {
            var close = GetNode<Button>("Margin/Root/Header/Close");
            var title = GetNode<Label>("Margin/Root/Header/Title");
            _search = GetNode<LineEdit>("Margin/Root/Header/Search");
            _sort = GetNode<OptionButton>("Margin/Root/Header/Sort");
            _refreshButton = GetNode<Button>("Margin/Root/Header/Refresh");
            _resultLabel = GetNode<Label>("Margin/Root/Header/Result");
            _grid = GetNode<ModGridView>("Margin/Root/Body/Content/Grid");
            _detail = GetNode<ModDetailView>("Margin/Root/Body/Content/Detail");
            _filterPanel = GetNode<ModFilterPanel>("Margin/Root/Body/Sidebar/SidebarScroll/FilterPanel");
            _bannerPanel = GetNode<PanelContainer>("Margin/Root/Banner");
            _bannerLabel = GetNode<Label>("Margin/Root/Banner/BannerLabel");

            close.Text = TranslationServer.Translate("mod_manager/back");
            title.Text = TranslationServer.Translate("mod_manager/title");
            _search.PlaceholderText = TranslationServer.Translate("mod_manager/search_placeholder");
            _refreshButton.Text = TranslationServer.Translate("mod_manager/refresh");
            _resultLabel.AddThemeColorOverride("font_color", UiTokens.Muted);

            _sort.AddItem(TranslationServer.Translate("mod_manager/sort_name"));
            _sort.AddItem(TranslationServer.Translate("mod_manager/sort_author"));
            _sort.AddItem(TranslationServer.Translate("mod_manager/sort_status"));
            _sort.Selected = (int)_filter.Sort;

            _bannerPanel.AddThemeStyleboxOverride("panel", ModManagerTheme.MakeBox(
                UiTokens.Alpha(UiTokens.Warning, 0.15f),
                UiTokens.Alpha(UiTokens.Warning, 0.45f),
                UiTokens.RadiusMd, UiTokens.Space4, UiTokens.Space2));
            _bannerLabel.AddThemeColorOverride("font_color", UiTokens.Warning);
            _bannerPanel.Visible = false;
            _detail.Visible = false;

            close.Pressed += OnClosePressed;
            _search.TextChanged += OnSearchChanged;
            _sort.ItemSelected += OnSortSelected;
            _refreshButton.Pressed += OnRefreshPressed;
            _grid.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _detail.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        private void SubscribeEvents()
        {
            _filterPanel.Changed += OnFilterChanged;
            _filterPanel.ResetRequested += OnFilterResetRequested;
            _grid.CardClicked += OnCardClicked;
            _grid.ActionPressed += OnActionPressed;
            _grid.EnabledToggled += OnEnabledToggled;
            _grid.VersionChanged += OnVersionChanged;
            _detail.BackRequested += OnDetailBackRequested;
            _detail.ActionPressed += OnActionPressed;
            _detail.DeleteRequested += OnDeleteRequested;
            _detail.EnabledToggled += OnEnabledToggled;
            _detail.VersionChanged += OnVersionChanged;
            ModDownloader.OnProgress += OnInstallProgress;
            CatalogService.OnRefreshed += OnCatalogRefreshed;
        }

        private void UnsubscribeEvents()
        {
            _filterPanel.Changed -= OnFilterChanged;
            _filterPanel.ResetRequested -= OnFilterResetRequested;
            _grid.CardClicked -= OnCardClicked;
            _grid.ActionPressed -= OnActionPressed;
            _grid.EnabledToggled -= OnEnabledToggled;
            _grid.VersionChanged -= OnVersionChanged;
            _detail.BackRequested -= OnDetailBackRequested;
            _detail.ActionPressed -= OnActionPressed;
            _detail.DeleteRequested -= OnDeleteRequested;
            _detail.EnabledToggled -= OnEnabledToggled;
            _detail.VersionChanged -= OnVersionChanged;
            ModDownloader.OnProgress -= OnInstallProgress;
            CatalogService.OnRefreshed -= OnCatalogRefreshed;
        }

        private void RefreshAll()
        {
            _model.Reload();
            RefreshView();
        }

        /// <summary>
        /// Rebuilds grid and counters from the current model without re-reading
        /// the registry/catalog. Filter, search and sort changes take this cheap
        /// path; install/delete/enable and catalog refreshes call
        /// <see cref="RefreshAll"/> first.
        /// </summary>
        private void RefreshView()
        {
            var items = _filter.Apply(_model.Items);
            _grid.SetItems(items, _filter.GroupByCategory);
            _filterPanel.RefreshCounts(_model.CountCategories(_filter), _model.CountStatuses(_filter));
            _resultLabel.Text = string.Format(TranslationServer.Translate("mod_manager/results"), items.Count);
            UpdateBanner();
        }

        private void UpdateBanner()
        {
            string text;
            if (_restartRequired)
                text = TranslationServer.Translate("mod_manager/restart_required");
            else if (CatalogService.IsFetching)
                text = TranslationServer.Translate("mod_manager/loading");
            else
                text = _statusMessage;

            SetBanner(text);
        }

        private void SetBanner(string text)
        {
            if (!GodotObject.IsInstanceValid(_bannerPanel))
                return;

            _bannerPanel.Visible = !string.IsNullOrEmpty(text);
            if (!string.IsNullOrEmpty(text))
                _bannerLabel.Text = text;
        }

        /// <summary>Finds the refreshed model row matching a card, including its catalog variant.</summary>
        private ModCardData FindItem(ModCardData data)
        {
            foreach (var item in _model.Items)
            {
                if (string.Equals(item.MatchKey, data.MatchKey, StringComparison.OrdinalIgnoreCase))
                    return item;
            }
            return null;
        }

        private void StartCatalogRefreshIfNeeded()
        {
            if (CatalogService.CachedEntries == null && ModConfig.Instance.CatalogUrls.Count > 0)
                RefreshCatalog();
            else
                UpdateBanner();
        }

        private async void RefreshCatalog()
        {
            if (GodotObject.IsInstanceValid(_refreshButton))
                _refreshButton.Disabled = true;
            UpdateBanner();

            try
            {
                await CatalogService.RefreshAsync(forceRefresh: true);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MOD_MANAGER", $"Catalog refresh failed: {ex.Message}", LogLevel.Warning);
            }

            if (!GodotObject.IsInstanceValid(this))
                return;

            if (GodotObject.IsInstanceValid(_refreshButton))
                _refreshButton.Disabled = false;
            UpdateBanner();
        }

        private void OnCatalogRefreshed()
        {
            if (!GodotObject.IsInstanceValid(this))
                return;

            RefreshAll();
        }

        private void OnInstallProgress(InstallProgress progress)
        {
            if (string.IsNullOrEmpty(_installingId) || progress == null)
                return;

            SetBanner(progress.Message);
        }

        private void OnClosePressed()
        {
            QueueFree();
        }

        private void OnDetailBackRequested()
        {
            _detail.Visible = false;
            _grid.Visible = true;
        }

        private void OnCardClicked(ModCardData data)
        {
            _detail.Open(data);
            _detail.Visible = true;
            _grid.Visible = false;
        }

        private void OnSearchChanged(string text)
        {
            _filter.Search = text;
            RefreshView();
        }

        private void OnSortSelected(long index)
        {
            _filter.Sort = (ModSortMode)(int)index;
            RefreshView();
        }

        private void OnFilterChanged()
        {
            RefreshView();
        }

        private void OnFilterResetRequested()
        {
            if (!string.IsNullOrEmpty(_search.Text))
                _search.Text = string.Empty;
        }

        private void OnVersionChanged(ModCardData data)
        {
            _filterPanel.RefreshCounts(_model.CountCategories(_filter), _model.CountStatuses(_filter));
        }

        private void OnActionPressed(ModCardData data)
        {
            var kind = ModActions.GetPrimary(data);
            if (kind == null)
                return;

            ExecuteAction(data, kind.Value);
        }

        private void OnDeleteRequested(ModCardData data)
        {
            ExecuteAction(data, ModActionKind.Delete);
        }

        private void OnEnabledToggled(ModCardData data)
        {
            _model.SetEnabled(data.Id, data.IsDisabled);
            _restartRequired = true;
            _statusMessage = null;
            RefreshAll();

            if (_detail.Visible)
            {
                var updated = FindItem(data);
                if (updated != null)
                    _detail.Open(updated);
                else
                    OnDetailBackRequested();
            }
        }

        private void ExecuteAction(ModCardData data, ModActionKind kind)
        {
            if (kind == ModActionKind.Delete)
            {
                ConfirmDelete(data);
                return;
            }

            StartInstall(data, kind);
        }

        private async void StartInstall(ModCardData data, ModActionKind kind)
        {
            if (!string.IsNullOrEmpty(_installingId))
                return;

            var version = data.SelectedDownload?.Version;
            _installingId = data.Id;
            _statusMessage = null;
            SetBanner(string.Format(TranslationServer.Translate("mod_manager/progress_installing"), data.Title));

            bool success;
            try
            {
                success = await ModDownloader.InstallAsync(data.Id, version, data.PrimarySource);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MOD_MANAGER", $"Install '{data.Id}' failed: {ex.Message}", LogLevel.Error);
                success = false;
            }

            _installingId = null;

            if (!GodotObject.IsInstanceValid(this))
                return;

            if (success)
            {
                _model.MarkInstalled(data.Id, version);
                _restartRequired = true;
                _statusMessage = null;
            }
            else
            {
                _statusMessage = TranslationServer.Translate("mod_manager/install_failed");
            }

            RefreshAll();

            if (_detail.Visible)
            {
                var updated = FindItem(data);
                if (updated != null)
                    _detail.Open(updated);
            }
        }

        private void ConfirmDelete(ModCardData data)
        {
            var dialog = new ConfirmationDialog
            {
                Title = TranslationServer.Translate("mod_manager/delete_title"),
                DialogText = string.Format(TranslationServer.Translate("mod_manager/delete_text"), data.Title),
                OkButtonText = TranslationServer.Translate("mod_manager/delete_confirm"),
                CancelButtonText = TranslationServer.Translate("mod_manager/cancel"),
                Exclusive = true,
            };

            var modId = data.Id;
            dialog.Confirmed += () =>
            {
                OnDeleteConfirmed(modId);
                dialog.QueueFree();
            };
            dialog.Canceled += dialog.QueueFree;

            AddChild(dialog);
            dialog.PopupCentered();
        }

        private void OnDeleteConfirmed(string modId)
        {
            if (!ModUninstaller.Uninstall(modId))
            {
                _statusMessage = TranslationServer.Translate("mod_manager/delete_failed");
                UpdateBanner();
                return;
            }

            _model.MarkRemoved(modId);
            _restartRequired = true;
            _statusMessage = null;

            if (_detail.Visible)
            {
                _detail.Visible = false;
                _grid.Visible = true;
            }

            RefreshAll();
        }

        private void OnRefreshPressed()
        {
            RefreshCatalog();
        }
    }
}
