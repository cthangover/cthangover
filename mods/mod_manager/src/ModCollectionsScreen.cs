using System;
using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.ModManager
{
    /// <summary>
    /// Fullscreen collections screen opened from the main menu. Shows every
    /// <see cref="ModCollection"/> known to the game (shipped inside mods,
    /// fetched from catalogs, or created by the player) as a dependency graph
    /// split into mechanics; the player picks a collection on the left, inspects
    /// mods on the graph and presses Apply to install and enable everything the
    /// collection needs.
    /// <para/>
    /// All heavy lifting lives in the core: <see cref="ModCollectionStore"/>
    /// merges the sources, <see cref="ModCollectionResolver"/> turns a collection
    /// into the graph, <see cref="ModCollectionPlan"/> decides what to download
    /// and enable. The screen only renders, runs downloads through
    /// <see cref="ModDownloader"/> and writes the enable set through
    /// <see cref="DisabledModsStore"/> — changes take effect after a restart,
    /// which the banner explains.
    /// </summary>
    public partial class ModCollectionsScreen : ModWidget
    {
        private Button _close;
        private Button _refresh;
        private Button _apply;
        private Label _result;
        private Label _detailTitle;
        private Label _detailMeta;
        private Label _detailDescription;
        private VBoxContainer _mechanicsList;
        private VBoxContainer _nodeBox;
        private VBoxContainer _planBox;
        private VBoxContainer _collectionList;
        private CollectionGraphView _graph;
        private PanelContainer _bannerPanel;
        private Label _bannerLabel;

        private List<ModCollection> _collections = new();
        private ModCollection _current;
        private ModCollectionGraph _graphData;
        private ModCollectionPlan _plan;
        private ButtonGroup _collectionGroup;

        /// <summary>Mods installed during this session; the registry only sees them after a restart.</summary>
        private readonly HashSet<string> _sessionInstalled = new(StringComparer.OrdinalIgnoreCase);
        private bool _installing;
        private bool _restartRequired;
        private string _statusMessage;

        protected override void Construct()
        {
            MouseFilter = MouseFilterEnum.Stop;
            CthangoverTheme.Apply(this);

            BindSceneNodes();
            SubscribeEvents();

            CatalogService.LoadCache();
            LoadCollections();
            StartCatalogRefreshIfNeeded();
        }

        protected override void Destruct()
        {
            UnsubscribeEvents();
        }

        private void BindSceneNodes()
        {
            _close = GetNode<Button>("Margin/Root/Header/Close");
            _refresh = GetNode<Button>("Margin/Root/Header/Refresh");
            _apply = GetNode<Button>("Margin/Root/Body/Details/DetailScroll/Detail/Apply");
            _result = GetNode<Label>("Margin/Root/Header/Result");
            _detailTitle = GetNode<Label>("Margin/Root/Body/Details/DetailScroll/Detail/DetailTitle");
            _detailMeta = GetNode<Label>("Margin/Root/Body/Details/DetailScroll/Detail/DetailMeta");
            _detailDescription = GetNode<Label>("Margin/Root/Body/Details/DetailScroll/Detail/DetailDescription");
            _mechanicsList = GetNode<VBoxContainer>("Margin/Root/Body/Details/DetailScroll/Detail/MechanicsList");
            _nodeBox = GetNode<VBoxContainer>("Margin/Root/Body/Details/DetailScroll/Detail/NodeBox");
            _planBox = GetNode<VBoxContainer>("Margin/Root/Body/Details/DetailScroll/Detail/PlanBox");
            _collectionList = GetNode<VBoxContainer>("Margin/Root/Body/Sidebar/SideScroll/CollectionList");
            _graph = GetNode<CollectionGraphView>("Margin/Root/Body/Graph");
            _bannerPanel = GetNode<PanelContainer>("Margin/Root/Banner");
            _bannerLabel = GetNode<Label>("Margin/Root/Banner/BannerLabel");

            SetCaption("Margin/Root/Header/Title", "mod_manager/collections_title");
            SetCaption("Margin/Root/Body/Details/DetailScroll/Detail/MechanicsCaption", "mod_manager/collection_mechanics");
            SetCaption("Margin/Root/Body/Details/DetailScroll/Detail/NodeCaption", "mod_manager/collection_node");
            SetCaption("Margin/Root/Body/Details/DetailScroll/Detail/PlanCaption", "mod_manager/collection_plan");

            _close.Text = TranslationServer.Translate("mod_manager/back");
            _refresh.Text = TranslationServer.Translate("mod_manager/refresh");
            _apply.Text = TranslationServer.Translate("mod_manager/collection_apply");
            _result.AddThemeColorOverride("font_color", UiTokens.Muted);
            _detailMeta.AddThemeColorOverride("font_color", UiTokens.Muted);
            _detailDescription.AddThemeColorOverride("font_color", UiTokens.Body);

            CthangoverTheme.ApplyPrimary(_apply);

            _bannerPanel.AddThemeStyleboxOverride("panel", ModManagerTheme.MakeBox(
                UiTokens.Alpha(UiTokens.Warning, 0.15f),
                UiTokens.Alpha(UiTokens.Warning, 0.45f),
                UiTokens.RadiusMd, UiTokens.Space4, UiTokens.Space2));
            _bannerLabel.AddThemeColorOverride("font_color", UiTokens.Warning);
            _bannerPanel.Visible = false;
        }

        private void SetCaption(string path, string localeKey)
        {
            var caption = GetNode<Label>(path);
            caption.Text = TranslationServer.Translate(localeKey);
            caption.AddThemeColorOverride("font_color", UiTokens.Muted);
        }

        private void SubscribeEvents()
        {
            _close.Pressed += OnClosePressed;
            _refresh.Pressed += OnRefreshPressed;
            _apply.Pressed += OnApplyPressed;
            _graph.NodeSelected += OnGraphNodeSelected;
            _graph.SelectionCleared += OnGraphSelectionCleared;
            ModDownloader.OnProgress += OnInstallProgress;
            CatalogService.OnRefreshed += OnCatalogRefreshed;
        }

        private void UnsubscribeEvents()
        {
            _close.Pressed -= OnClosePressed;
            _refresh.Pressed -= OnRefreshPressed;
            _apply.Pressed -= OnApplyPressed;
            _graph.NodeSelected -= OnGraphNodeSelected;
            _graph.SelectionCleared -= OnGraphSelectionCleared;
            ModDownloader.OnProgress -= OnInstallProgress;
            CatalogService.OnRefreshed -= OnCatalogRefreshed;
        }

        private void LoadCollections()
        {
            _collections = ModCollectionStore.LoadAll();
            _collections.Sort(CompareCollections);

            _current = FindCollection(_current?.Id) ?? (_collections.Count > 0 ? _collections[0] : null);
            RebuildCollectionList();
            RenderCurrent();
        }

        private ModCollection FindCollection(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            foreach (var collection in _collections)
            {
                if (string.Equals(collection.Id, id, StringComparison.OrdinalIgnoreCase))
                    return collection;
            }

            return null;
        }

        private static int CompareCollections(ModCollection a, ModCollection b)
        {
            if (a.Origin != b.Origin)
                return (int)a.Origin - (int)b.Origin;
            return string.Compare(a.DisplayTitle, b.DisplayTitle, StringComparison.OrdinalIgnoreCase);
        }

        private void RebuildCollectionList()
        {
            ClearChildren(_collectionList);
            _collectionGroup = new ButtonGroup();

            foreach (var collection in _collections)
            {
                var button = new Button
                {
                    Text = collection.DisplayTitle,
                    TooltipText = collection.Description,
                    ToggleMode = true,
                    ButtonGroup = _collectionGroup,
                    Alignment = HorizontalAlignment.Left,
                    ButtonPressed = _current != null
                        && string.Equals(collection.Id, _current.Id, StringComparison.OrdinalIgnoreCase),
                };

                var id = collection.Id;
                button.Pressed += () => OnCollectionPressed(id);
                _collectionList.AddChild(button);
            }
        }

        private void OnCollectionPressed(string id)
        {
            _current = FindCollection(id);
            RenderCurrent();
        }

        private void RenderCurrent()
        {
            if (_current == null)
            {
                _graph.Clear();
                _detailTitle.Text = TranslationServer.Translate("mod_manager/collections_empty");
                _detailMeta.Text = string.Empty;
                _detailDescription.Text = string.Empty;
                ClearChildren(_mechanicsList);
                ClearChildren(_nodeBox);
                ClearChildren(_planBox);
                _result.Text = string.Empty;
                _plan = null;
                UpdateApplyButton();
                UpdateBanner();
                return;
            }

            var catalog = CatalogService.CachedEntries ?? new List<CatalogExportEntry>();
            _graphData = ModCollectionResolver.Resolve(
                _current,
                ModRegistry.Instance.AllDiscoveredMods,
                DisabledModsStore.Load(),
                catalog,
                _sessionInstalled);
            _plan = ModCollectionPlan.Build(_graphData, catalog);

            _graph.Populate(_graphData);
            RenderCollectionDetails();
            RenderNodeDetails(null);
            RenderPlan();
            UpdateApplyButton();
            UpdateBanner();
        }

        private void RenderCollectionDetails()
        {
            _detailTitle.Text = _current.DisplayTitle;
            _detailMeta.Text = BuildCollectionMeta();
            _detailDescription.Text = string.IsNullOrWhiteSpace(_current.Description)
                ? "—"
                : _current.Description;

            ClearChildren(_mechanicsList);
            foreach (var mechanic in _graphData.Mechanics)
            {
                var count = CountMechanicMods(mechanic.Id);
                var optional = mechanic.Optional
                    ? $"  ·  {TranslationServer.Translate("mod_manager/collection_optional")}"
                    : string.Empty;
                var name = string.IsNullOrEmpty(mechanic.Name) ? mechanic.Id : mechanic.Name;

                var label = MakeLabel($"{name}{optional}  ·  {count}", UiTokens.Ink);
                label.TooltipText = mechanic.Description;
                _mechanicsList.AddChild(label);
            }

            _result.Text = string.Format(
                TranslationServer.Translate("mod_manager/collection_result"),
                _graphData.InstalledCount,
                _graphData.TotalCount);
        }

        private string BuildCollectionMeta()
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(_current.Version))
                parts.Add($"v{_current.Version}");
            if (!string.IsNullOrEmpty(_current.Author))
                parts.Add(_current.Author);
            parts.Add(TranslationServer.Translate(OriginKey(_current.Origin)));
            return string.Join("  ·  ", parts);
        }

        private static string OriginKey(ModCollectionOrigin origin) => origin switch
        {
            ModCollectionOrigin.User => "mod_manager/collection_origin_user",
            ModCollectionOrigin.Catalog => "mod_manager/collection_origin_catalog",
            _ => "mod_manager/collection_origin_builtin",
        };

        private int CountMechanicMods(string mechanicId)
        {
            var count = 0;
            foreach (var node in _graphData.Nodes)
            {
                if (string.Equals(node.MechanicId, mechanicId, StringComparison.OrdinalIgnoreCase))
                    count++;
            }

            return count;
        }

        private void RenderNodeDetails(ModCollectionNode node)
        {
            ClearChildren(_nodeBox);

            if (node == null)
            {
                _nodeBox.AddChild(MakeLabel(
                    TranslationServer.Translate("mod_manager/collection_node_hint"), UiTokens.Faint));
                return;
            }

            _nodeBox.AddChild(MakeLabel(node.Title, UiTokens.InkBright));

            var meta = new List<string> { node.ModId };
            if (node.IsExternal)
                meta.Add(TranslationServer.Translate("mod_manager/collection_external"));
            _nodeBox.AddChild(MakeLabel(string.Join("  ·  ", meta), UiTokens.Muted));

            var state = MakeLabel(CollectionStateNames.Translate(node.State), CollectionStateNames.Color(node.State));
            _nodeBox.AddChild(state);

            if (!string.IsNullOrEmpty(node.RequiredVersion))
            {
                _nodeBox.AddChild(MakeLabel(
                    string.Format(TranslationServer.Translate("mod_manager/collection_requires"), node.RequiredVersion),
                    UiTokens.Muted));
            }

            if (!string.IsNullOrEmpty(node.InstalledVersion))
            {
                _nodeBox.AddChild(MakeLabel(
                    string.Format(TranslationServer.Translate("mod_manager/installed_version"), node.InstalledVersion),
                    UiTokens.Muted));
            }

            AddEdgeList("mod_manager/depends", node, dependencies: true);
            AddEdgeList("mod_manager/used_by", node, dependencies: false);
        }

        private void AddEdgeList(string captionKey, ModCollectionNode node, bool dependencies)
        {
            var names = new List<string>();
            foreach (var edge in _graphData.Edges)
            {
                var match = dependencies
                    ? string.Equals(edge.ToModId, node.ModId, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(edge.FromModId, node.ModId, StringComparison.OrdinalIgnoreCase);
                if (!match)
                    continue;

                var otherId = dependencies ? edge.FromModId : edge.ToModId;
                var title = FindNodeTitle(otherId);
                names.Add(title == otherId ? otherId : $"{title} ({otherId})");
            }

            if (names.Count == 0)
                return;

            _nodeBox.AddChild(MakeLabel(
                $"{TranslationServer.Translate(captionKey)}: {string.Join(", ", names)}", UiTokens.Body));
        }

        private string FindNodeTitle(string modId)
        {
            foreach (var node in _graphData.Nodes)
            {
                if (string.Equals(node.ModId, modId, StringComparison.OrdinalIgnoreCase))
                    return node.Title;
            }

            return modId;
        }

        private void RenderPlan()
        {
            ClearChildren(_planBox);

            if (_plan == null)
                return;

            if (_plan.IsEmpty && !_plan.HasProblems)
            {
                _planBox.AddChild(MakeLabel(
                    TranslationServer.Translate("mod_manager/collection_plan_ready"), UiTokens.Success));
                return;
            }

            if (_plan.Install.Count > 0)
            {
                _planBox.AddChild(MakeLabel(
                    string.Format(TranslationServer.Translate("mod_manager/collection_install_count"), _plan.Install.Count),
                    UiTokens.Ink));
                foreach (var item in _plan.Install)
                {
                    var version = string.IsNullOrEmpty(item.Version) ? string.Empty : $" v{item.Version}";
                    _planBox.AddChild(MakeLabel($"  {item.ModId}{version}", UiTokens.Info));
                }
            }

            if (_plan.Enable.Count > 0)
            {
                _planBox.AddChild(MakeLabel(
                    string.Format(TranslationServer.Translate("mod_manager/collection_enable_count"), _plan.Enable.Count),
                    UiTokens.Ink));
                foreach (var id in _plan.Enable)
                    _planBox.AddChild(MakeLabel($"  {id}", UiTokens.Warning));
            }

            if (_plan.Missing.Count > 0)
            {
                _planBox.AddChild(MakeLabel(
                    string.Format(TranslationServer.Translate("mod_manager/collection_missing"), string.Join(", ", _plan.Missing)),
                    UiTokens.Danger));
            }

            if (_plan.Mismatches.Count > 0)
            {
                _planBox.AddChild(MakeLabel(
                    string.Format(TranslationServer.Translate("mod_manager/collection_mismatch"), string.Join(", ", _plan.Mismatches)),
                    UiTokens.Warning));
            }
        }

        private void UpdateApplyButton()
        {
            var canApply = !_installing
                && _plan != null
                && !_plan.IsEmpty
                && _plan.Missing.Count == 0;
            _apply.Disabled = !canApply;
        }

        private void OnGraphNodeSelected(ModCollectionNode node)
        {
            RenderNodeDetails(node);
        }

        private void OnGraphSelectionCleared()
        {
            RenderNodeDetails(null);
        }

        private void OnClosePressed()
        {
            QueueFree();
        }

        private void OnRefreshPressed()
        {
            RefreshCatalog();
        }

        private async void RefreshCatalog()
        {
            if (GodotObject.IsInstanceValid(_refresh))
                _refresh.Disabled = true;
            UpdateBanner();

            try
            {
                await CatalogService.RefreshAsync(forceRefresh: true);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MOD_COLLECTIONS", $"Catalog refresh failed: {ex.Message}", LogLevel.Warning);
            }

            if (!GodotObject.IsInstanceValid(this))
                return;

            if (GodotObject.IsInstanceValid(_refresh))
                _refresh.Disabled = false;
            UpdateBanner();
        }

        private void OnCatalogRefreshed()
        {
            if (!GodotObject.IsInstanceValid(this))
                return;

            LoadCollections();
        }

        private void OnInstallProgress(InstallProgress progress)
        {
            if (!_installing || progress == null)
                return;

            SetBanner(progress.Message);
        }

        private async void OnApplyPressed()
        {
            if (_installing || _current == null || _plan == null || _plan.IsEmpty || _plan.Missing.Count > 0)
                return;

            var plan = _plan;
            _installing = true;
            _statusMessage = null;
            SetBanner(string.Format(
                TranslationServer.Translate("mod_manager/collection_progress"), _current.DisplayTitle));
            UpdateApplyButton();

            var failed = false;
            foreach (var item in plan.Install)
            {
                bool success;
                try
                {
                    success = await ModDownloader.InstallAsync(item.ModId, item.Version, item.SourceUrl);
                }
                catch (Exception ex)
                {
                    GameLogger.Log("MOD_COLLECTIONS", $"Install '{item.ModId}' failed: {ex.Message}", LogLevel.Error);
                    success = false;
                }

                if (!success)
                {
                    failed = true;
                    break;
                }

                _sessionInstalled.Add(item.ModId);
            }

            if (!failed)
            {
                var disabled = DisabledModsStore.Load();
                foreach (var id in plan.Enable)
                    disabled.Remove(id);
                DisabledModsStore.Save(disabled);

                _restartRequired = true;
                _statusMessage = null;
            }
            else
            {
                _statusMessage = TranslationServer.Translate("mod_manager/collection_apply_failed");
            }

            _installing = false;

            if (!GodotObject.IsInstanceValid(this))
                return;

            RenderCurrent();
        }

        private void StartCatalogRefreshIfNeeded()
        {
            if (CatalogService.CachedEntries == null && ModConfig.Instance.CatalogUrls.Count > 0)
                RefreshCatalog();
            else
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

        private static void ClearChildren(Node container)
        {
            foreach (var child in container.GetChildren())
            {
                container.RemoveChild(child);
                child.QueueFree();
            }
        }

        private static Label MakeLabel(string text, Color color)
        {
            var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            label.AddThemeColorOverride("font_color", color);
            return label;
        }
    }
}
