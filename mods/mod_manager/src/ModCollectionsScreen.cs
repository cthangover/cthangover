using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.Utils;
using Godot;

using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Mods.Interface.Theming;
namespace Mods.ModManager
{
    /// <summary>
    /// Fullscreen scheme editor opened from the main menu: the whole game on one
    /// canvas exactly like the approved mockup. The header picks the game and
    /// creates new games/mechanics, the canvas shows movable and resizable
    /// mechanic contours with freely placed mod chips and dependency lines, and
    /// the inspector edits the selected group or mod.
    /// <para/>
    /// All heavy lifting stays in the core: <see cref="ModCollectionStore"/>
    /// merges documents, <see cref="ModCollectionResolver"/> expands includes
    /// into groups and problems, <see cref="ModCollectionPlan"/> decides what to
    /// download and enable, and <see cref="CollectionLayoutStore"/> persists the
    /// arrangement. Edits mutate drafts in <c>_overrides</c> — never the loaded
    /// documents — and only "Сохранить" writes them back through
    /// <see cref="ModCollectionStore.SaveUser"/>, so the player can explore
    /// layouts without committing anything.
    /// </summary>
    public partial class ModCollectionsScreen : ModWidget
    {
        private const string RoleExtra = "extra";

        private Button _close;
        private Button _refresh;
        private Button _addMechanic;
        private Button _newGame;
        private Button _apply;
        private OptionButton _gameSelect;
        private CheckBox _edgesToggle;
        private Button _pillProblems;
        private Button _pillPlan;
        private Label _modsCount;
        private Label _dirty;
        private Button _json;
        private Button _reset;
        private Button _save;
        private Label _bannerLabel;
        private Label _detailTitle;
        private Label _detailMeta;
        private Label _detailDescription;
        private VBoxContainer _detailExtra;
        private PanelContainer _bannerPanel;
        private CollectionSchemeCanvas _canvas;
        private Button _tabInspector;
        private Button _tabProblems;
        private Button _tabPlan;
        private HBoxContainer _crumbs;
        private Button _crumbCollections;
        private Label _buildName;
        private Label _buildInstall;
        private Label _buildEnable;
        private Label _buildProblems;
        private Label _buildHint;
        private Button _buildManager;
        private Button _buildApply;

        private List<ModCollection> _collections = new();
        private readonly Dictionary<string, ModCollection> _overrides = new(StringComparer.OrdinalIgnoreCase);
        private string _currentId;
        private ModCollectionGraph _graph;
        private ModCollectionPlan _plan;
        private ModCollectionLayout _layout = new();
        private string _selectedGroupKey;
        private string _selectedModId;
        private string _tab = "inspector";

        /// <summary>Mods installed during this session; the registry only sees them after a restart.</summary>
        private HashSet<string> _sessionInstalled = new(StringComparer.OrdinalIgnoreCase);
        private bool _installing;
        private bool _restartRequired;
        private string _statusMessage;

        /// <summary>
        /// When true the editor runs as the second level of the collections
        /// tab: the close/title/new-game/catalog-refresh header controls are
        /// hidden (the hosting <see cref="ModHubScreen"/> owns navigation) and
        /// the game selector moves into the breadcrumb row. Editing, saving
        /// and applying behaviour are unchanged.
        /// </summary>
        public bool Embedded { get; set; }

        /// <summary>Raised when the breadcrumbs ask to go back to the games gallery.</summary>
        public event Action GalleryRequested;

        /// <summary>Raised when a "not found" problem asks to search the mod catalog; the argument is the missing id.</summary>
        public event Action<string> OpenManagerRequested;

        /// <summary>Raised after every rebuild so the hosting hub can refresh its header.</summary>
        public event Action ViewChanged;

        /// <summary>Localized, '@key'-resolved title of the collection being edited; null when nothing is selected.</summary>
        public string CurrentCollectionTitle
        {
            get
            {
                var collection = EffectiveCollection(_currentId);
                if (collection == null)
                    return null;

                var raw = collection.GetLocalizedName(TranslationServer.GetLocale());
                return CollectionText.Resolve(raw);
            }
        }

        /// <summary>Shares the hub-wide set of mods installed this session so gallery and editor stay consistent.</summary>
        public void SetSessionInstalled(HashSet<string> sessionInstalled)
            => _sessionInstalled = sessionInstalled ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Opens the scheme editor on the given collection id, falling back to
        /// the first available game when the id is null or unknown. Selection
        /// is cleared — the same fresh view a game-switch produces.
        /// </summary>
        public void OpenCollection(string collectionId)
        {
            _currentId = collectionId;
            _selectedGroupKey = null;
            _selectedModId = null;
            if (string.IsNullOrEmpty(_currentId) || EffectiveCollection(_currentId) == null)
                _currentId = FirstGameId();

            Rebuild();
        }

        /// <summary>Creates a fresh player game draft and opens it — the gallery's "build your own game" action.</summary>
        public void OpenNewGame() => OnNewGamePressed();

        protected override void Construct()
        {
            MouseFilter = MouseFilterEnum.Stop;
            CthangoverTheme.Apply(this);

            BindSceneNodes();
            SubscribeEvents();

            CatalogService.LoadCache();
            ReloadCollections();
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
            _addMechanic = GetNode<Button>("Margin/Root/Header/AddMechanic");
            _newGame = GetNode<Button>("Margin/Root/Header/NewGame");
            _gameSelect = GetNode<OptionButton>("Margin/Root/Header/GameSelect");
            _edgesToggle = GetNode<CheckBox>("Margin/Root/Header/EdgesToggle");
            _pillProblems = GetNode<Button>("Margin/Root/Header/Problems");
            _pillPlan = GetNode<Button>("Margin/Root/Header/Plan");
            _modsCount = GetNode<Label>("Margin/Root/Header/ModsCount");
            _dirty = GetNode<Label>("Margin/Root/Header/Dirty");
            _json = GetNode<Button>("Margin/Root/Header/Json");
            _reset = GetNode<Button>("Margin/Root/Header/Reset");
            _save = GetNode<Button>("Margin/Root/Header/Save");
            _bannerPanel = GetNode<PanelContainer>("Margin/Root/Banner");
            _bannerLabel = GetNode<Label>("Margin/Root/Banner/BannerLabel");
            _canvas = GetNode<CollectionSchemeCanvas>("Margin/Root/Body/CanvasScroll/SchemeCanvas");

            var detail = "Margin/Root/Body/Details/DetailScroll/Detail/";
            _detailTitle = GetNode<Label>(detail + "DetailTitle");
            _detailMeta = GetNode<Label>(detail + "DetailMeta");
            _detailDescription = GetNode<Label>(detail + "DetailDescription");
            _detailExtra = GetNode<VBoxContainer>(detail + "DetailExtra");
            _apply = GetNode<Button>(detail + "Apply");

            _tabInspector = GetNode<Button>(detail + "Tabs/TabInspector");
            _tabProblems = GetNode<Button>(detail + "Tabs/TabProblems");
            _tabPlan = GetNode<Button>(detail + "Tabs/TabPlan");

            _close.Text = TranslationServer.Translate("mod_manager/back");
            _refresh.Text = TranslationServer.Translate("mod_manager/refresh");
            _addMechanic.Text = TranslationServer.Translate("mod_manager/collection_add_mechanic");
            _newGame.Text = TranslationServer.Translate("mod_manager/collection_new_game");
            _pillProblems.Text = TranslationServer.Translate("mod_manager/collection_problems");
            _pillPlan.Text = TranslationServer.Translate("mod_manager/collection_plan");
            _dirty.Text = TranslationServer.Translate("mod_manager/collection_unsaved");
            _json.Text = TranslationServer.Translate("mod_manager/collection_json");
            _reset.Text = TranslationServer.Translate("mod_manager/collection_reset");
            _save.Text = TranslationServer.Translate("mod_manager/collection_save");
            _tabInspector.Text = TranslationServer.Translate("mod_manager/collection_tab_inspector");
            _tabProblems.Text = TranslationServer.Translate("mod_manager/collection_tab_problems");
            _tabPlan.Text = TranslationServer.Translate("mod_manager/collection_tab_plan");
            _apply.Text = TranslationServer.Translate("mod_manager/collection_apply");
            _edgesToggle.Text = TranslationServer.Translate("mod_manager/collection_links");

            CthangoverTheme.ApplyPrimary(_apply);
            CthangoverTheme.ApplyPrimary(_save);
            _dirty.AddThemeColorOverride("font_color", UiTokens.Warning);
            _detailMeta.AddThemeColorOverride("font_color", UiTokens.Muted);
            _detailDescription.AddThemeColorOverride("font_color", UiTokens.Body);

            _bannerPanel.AddThemeStyleboxOverride("panel", ModManagerTheme.MakeBox(
                UiTokens.Alpha(UiTokens.Warning, 0.15f),
                UiTokens.Alpha(UiTokens.Warning, 0.45f),
                UiTokens.RadiusMd, UiTokens.Space4, UiTokens.Space2));
            _bannerLabel.AddThemeColorOverride("font_color", UiTokens.Warning);
            _bannerPanel.Visible = false;

            BuildCrumbs();
            BuildBuildBar();
            if (Embedded)
                ApplyEmbeddedLayout();
        }

        /// <summary>
        /// Fills the breadcrumb row: "Collections › [game selector] › Scheme"
        /// plus the scheme controls that used to crowd the header. The game
        /// selector node itself is reparented here so its items and events
        /// keep working unchanged.
        /// </summary>
        private void BuildCrumbs()
        {
            _crumbs = GetNode<HBoxContainer>("Margin/Root/Crumbs");
            _crumbs.AddThemeConstantOverride("separation", UiTokens.Space2);

            _crumbCollections = UiKit.Button(TranslationServer.Translate("mod_manager/hub_tab_collections"));
            _crumbCollections.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);
            _crumbCollections.Pressed += () => GalleryRequested?.Invoke();
            _crumbCollections.TooltipText = TranslationServer.Translate("mod_manager/gallery_crumb");
            _crumbs.AddChild(_crumbCollections);
            _crumbs.AddChild(UiKit.Label("›", UiTokens.FontBodySmall, UiTokens.Faint));

            GetNode("Margin/Root/Header/GameSelect").Reparent(_crumbs);
            _crumbs.AddChild(UiKit.Label("›", UiTokens.FontBodySmall, UiTokens.Faint));
            _crumbs.AddChild(UiKit.Label(TranslationServer.Translate("mod_manager/collection_crumb_scheme"), UiTokens.FontBodySmall, UiTokens.Body));

            _crumbs.AddChild(UiKit.Spacer());
            GetNode("Margin/Root/Header/AddMechanic").Reparent(_crumbs);
            GetNode("Margin/Root/Header/EdgesToggle").Reparent(_crumbs);
            GetNode("Margin/Root/Header/Problems").Reparent(_crumbs);
            GetNode("Margin/Root/Header/ModsCount").Reparent(_crumbs);
        }

        /// <summary>
        /// Builds the permanent bottom build bar from the mockup: build name,
        /// install/enable/problems counters, a hint and the apply + open
        /// manager actions. Replaces the old plan-tab-only apply button as the
        /// always-visible place to run the plan.
        /// </summary>
        private void BuildBuildBar()
        {
            var bar = GetNode<PanelContainer>("Margin/Root/BuildBar");
            bar.AddThemeStyleboxOverride("panel", UiKit.Frame(0, UiTokens.Surface2, UiTokens.Line));
            bar.CustomMinimumSize = new Vector2(0, 54);
            var row = GetNode<HBoxContainer>("Margin/Root/BuildBar/BarRow");
            row.AddThemeConstantOverride("separation", UiTokens.Space5);

            var nameColumn = UiKit.Column(0);
            string buildCaption = TranslationServer.Translate("mod_manager/build_caption");
            nameColumn.AddChild(UiKit.Label(buildCaption.ToUpperInvariant(), UiTokens.FontMicro, UiTokens.Faint));
            _buildName = UiKit.Label(string.Empty, UiTokens.FontBodyLarge, UiTokens.InkBright);
            nameColumn.AddChild(_buildName);
            row.AddChild(nameColumn);

            row.AddChild(BuildStat(TranslationServer.Translate("mod_manager/build_stat_install"), out _buildInstall));
            row.AddChild(BuildStat(TranslationServer.Translate("mod_manager/build_stat_enable"), out _buildEnable));
            row.AddChild(BuildStat(TranslationServer.Translate("mod_manager/build_stat_problems"), out _buildProblems));

            _buildHint = UiKit.Label(string.Empty, UiTokens.FontBodySmall, UiTokens.Warning);
            _buildHint.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(_buildHint);
            row.AddChild(UiKit.Spacer());

            _buildManager = UiKit.Button(TranslationServer.Translate("mod_manager/build_open_manager"));
            _buildManager.Pressed += () => OpenManagerRequested?.Invoke(null);
            row.AddChild(_buildManager);

            _buildApply = UiKit.Button(TranslationServer.Translate("mod_manager/collection_apply"), ButtonKind.Primary);
            _buildApply.Pressed += OnApplyPressed;
            row.AddChild(_buildApply);
        }

        /// <summary>One "k/v" counter column of the build bar; the value label is returned to the caller.</summary>
        private Control BuildStat(string caption, out Label value)
        {
            var column = UiKit.Column(0);
            column.AddChild(UiKit.Label(caption.ToUpperInvariant(), UiTokens.FontMicro, UiTokens.Faint));
            value = UiKit.Label("0", UiTokens.FontBodyLarge, UiTokens.InkBright);
            column.AddChild(value);
            return column;
        }

        /// <summary>Slims the header down to save/reset/json/dirty as the mockup requires.</summary>
        private void ApplyEmbeddedLayout()
        {
            GetNode<Label>("Margin/Root/Header/Title").Visible = false;
            _close.Visible = false;
            _refresh.Visible = false;
            _newGame.Visible = false;
        }

        private void SubscribeEvents()
        {
            _close.Pressed += OnClosePressed;
            _refresh.Pressed += OnRefreshPressed;
            _addMechanic.Pressed += OnAddMechanicPressed;
            _newGame.Pressed += OnNewGamePressed;
            _edgesToggle.Toggled += _canvas.SetEdgesVisible;
            _pillProblems.Pressed += () => SelectTab("problems");
            _pillPlan.Pressed += () => SelectTab("plan");
            _json.Pressed += OpenJsonDialog;
            _reset.Pressed += ResetDrafts;
            _save.Pressed += OpenSaveDialog;
            _gameSelect.ItemSelected += OnGameSelected;
            _tabInspector.Pressed += () => SelectTab("inspector");
            _tabProblems.Pressed += () => SelectTab("problems");
            _tabPlan.Pressed += () => SelectTab("plan");
            _apply.Pressed += OnApplyPressed;

            _canvas.GroupSelected += OnGroupSelected;
            _canvas.ModSelected += OnModSelected;
            _canvas.GroupCommand += OnGroupCommand;
            _canvas.LayoutChanged += OnLayoutChanged;

            ModDownloader.OnProgress += OnInstallProgress;
            CatalogService.OnRefreshed += OnCatalogRefreshed;
        }

        private void UnsubscribeEvents()
        {
            ModDownloader.OnProgress -= OnInstallProgress;
            CatalogService.OnRefreshed -= OnCatalogRefreshed;
        }

        /* ═══════════════ Data flow ═══════════════ */

        private void ReloadCollections()
        {
            _collections = ModCollectionStore.LoadAll();

            if (string.IsNullOrEmpty(_currentId) || EffectiveCollection(_currentId) == null)
                _currentId = FirstGameId();

            RebuildGameSelect();
            Rebuild();
        }

        private void Rebuild()
        {
            _graph = Resolve();
            _layout = _currentId == null ? new ModCollectionLayout() : CollectionLayoutStore.Load(_currentId);
            _plan = ModCollectionPlan.Build(_graph, CatalogService.CachedEntries ?? new List<CatalogExportEntry>());

            _canvas.Build(_graph, _layout);
            _canvas.SetSelection(_selectedGroupKey, _selectedModId);
            _canvas.SetEdgesVisible(_edgesToggle.ButtonPressed);

            UpdateHeader();
            UpdateInspector();
            UpdateBanner();
            UpdateBuildBar();
            ViewChanged?.Invoke();
        }

        private ModCollectionGraph Resolve()
        {
            var game = EffectiveCollection(_currentId);
            if (game == null)
                return new ModCollectionGraph();

            return ModCollectionResolver.Resolve(
                game,
                BuildCollectionIndex(),
                ModRegistry.Instance.AllDiscoveredMods,
                DisabledModsStore.Load(),
                CatalogService.CachedEntries ?? new List<CatalogExportEntry>(),
                _sessionInstalled,
                TranslationServer.GetLocale(),
                CollectionText.Resolve);
        }

        private Dictionary<string, ModCollection> BuildCollectionIndex()
        {
            var index = new Dictionary<string, ModCollection>(StringComparer.OrdinalIgnoreCase);
            foreach (var collection in _collections)
            {
                if (collection?.Id != null)
                    index[collection.Id] = collection;
            }

            foreach (var pair in _overrides)
                index[pair.Key] = pair.Value;

            return index;
        }

        private ModCollection EffectiveCollection(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            if (_overrides.TryGetValue(id, out var draft))
                return draft;

            return _collections.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        private ModCollection OriginalCollection(string id)
            => _collections.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>Returns the editable draft of a collection, cloning the loaded document on first touch.</summary>
        private ModCollection Draft(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            if (_overrides.TryGetValue(id, out var draft))
                return draft;

            var source = OriginalCollection(id);
            if (source == null)
                return null;

            draft = CloneCollection(source);
            _overrides[id] = draft;
            return draft;
        }

        private ModCollection DraftOfGroup(ModCollectionGroup group)
        {
            if (group == null)
                return null;

            return Draft(string.IsNullOrEmpty(group.CollectionId) ? group.ParentCollectionId : group.CollectionId);
        }

        private static ModCollection CloneCollection(ModCollection source)
        {
            var json = JsonSerializer.Serialize(source, JsonOptionsProvider.WriteIndentedOptions);
            return JsonSerializer.Deserialize<ModCollection>(json, JsonOptionsProvider.ReadOptions);
        }

        private string FirstGameId()
        {
            var game = _collections
                .Where(c => c.Kind == ModCollectionKind.Game)
                .OrderBy(c => c.Name ?? c.Id, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            return game?.Id ?? _collections.FirstOrDefault()?.Id;
        }

        /* ═══════════════ Header ═══════════════ */

        private void RebuildGameSelect()
        {
            _gameSelect.Clear();
            var language = TranslationServer.GetLocale();
            var games = _collections
                .Where(c => c.Kind == ModCollectionKind.Game)
                .OrderBy(c => c.GetLocalizedName(language), StringComparer.OrdinalIgnoreCase)
                .ToList();

            var selected = 0;
            for (var i = 0; i < games.Count; i++)
            {
                _gameSelect.AddItem(games[i].GetLocalizedName(language));
                _gameSelect.SetItemMetadata(i, games[i].Id);
                if (string.Equals(games[i].Id, _currentId, StringComparison.OrdinalIgnoreCase))
                    selected = i;
            }

            if (games.Count > 0)
                _gameSelect.Selected = selected;
        }

        private void UpdateHeader()
        {
            var dirty = _overrides.Count > 0;
            _dirty.Visible = dirty;
            _save.Disabled = !dirty;
            _reset.Disabled = !dirty;

            if (_graph != null)
            {
                var installed = _graph.Nodes.Count(n => n.State == ModCollectionNodeState.Installed);
                _modsCount.Text = string.Format(
                    TranslationServer.Translate("mod_manager/collection_result"), installed, _graph.Nodes.Count);
            }
            else
            {
                _modsCount.Text = string.Empty;
            }

            var errors = CountErrors();
            _pillProblems.Text = errors > 0
                ? $"{TranslationServer.Translate("mod_manager/collection_problems")}: {errors}"
                : TranslationServer.Translate("mod_manager/collection_problems");

            if (_plan == null)
            {
                _pillPlan.Text = TranslationServer.Translate("mod_manager/collection_plan");
                return;
            }

            var parts = new List<string>();
            if (_plan.Install.Count > 0)
                parts.Add($"{TranslationServer.Translate("mod_manager/collection_plan_install")}: {_plan.Install.Count}");
            if (_plan.Enable.Count > 0)
                parts.Add($"{TranslationServer.Translate("mod_manager/collection_plan_enable")}: {_plan.Enable.Count}");
            _pillPlan.Text = parts.Count > 0
                ? $"{TranslationServer.Translate("mod_manager/collection_plan")}: {string.Join(" · ", parts)}"
                : TranslationServer.Translate("mod_manager/collection_plan_ready");
        }

        private int CountErrors()
            => _graph?.Problems?.Count ?? 0;

        private void OnGameSelected(long index)
        {
            var id = _gameSelect.GetItemMetadata((int)index).AsString();
            if (string.IsNullOrEmpty(id) || string.Equals(id, _currentId, StringComparison.OrdinalIgnoreCase))
                return;

            _currentId = id;
            _selectedGroupKey = null;
            _selectedModId = null;
            Rebuild();
        }

        private void OnNewGamePressed()
        {
            var id = UniqueGameId();
            var draft = new ModCollection
            {
                Id = id,
                Kind = ModCollectionKind.Game,
                Name = TranslationServer.Translate("mod_manager/collection_new_game_name"),
                Author = "player",
                Version = "0.1.0",
                Description = TranslationServer.Translate("mod_manager/collection_new_game_desc"),
                Tags = new List<string>(),
                Provides = new List<string>(),
                Requires = new List<string>(),
                Includes = new List<ModCollectionInclude>(),
                Mechanics = new List<ModCollectionMechanic>(),
            };

            _overrides[id] = draft;
            _currentId = id;
            _selectedGroupKey = null;
            _selectedModId = null;

            RebuildGameSelect();
            Rebuild();
            OpenSaveDialog();
        }

        private string UniqueGameId()
        {
            var index = 1;
            while (EffectiveCollection($"my_game_{index}") != null)
                index++;
            return $"my_game_{index}";
        }

        /* ═══════════════ Canvas events ═══════════════ */

        private void OnGroupSelected(string groupKey)
        {
            _selectedGroupKey = groupKey;
            _selectedModId = null;
            _canvas.SetSelection(_selectedGroupKey, _selectedModId);
            SelectTab("inspector");
        }

        private void OnModSelected(string modId)
        {
            _selectedModId = modId;
            _selectedGroupKey = _canvas.GetGroupKeyOfMod(modId);
            _canvas.SetSelection(_selectedGroupKey, _selectedModId);
            SelectTab("inspector");
        }

        private void OnGroupCommand(ModCollectionGroup group, string command)
        {
            switch (command)
            {
                case "add-mod":
                    OpenModPicker(group);
                    break;
                case "rename":
                    OpenRenameDialog(group);
                    break;
                case "toggle":
                    ToggleGroup(group);
                    break;
                case "replace":
                    OpenMechanicPicker(group, true);
                    break;
                case "remove":
                    RemoveGroup(group);
                    break;
            }
        }

        private void OnLayoutChanged()
        {
            if (!string.IsNullOrEmpty(_currentId))
                CollectionLayoutStore.Save(_currentId, _layout);
        }

        private void ToggleGroup(ModCollectionGroup group)
        {
            if (group.IsInclude)
            {
                var include = FindInclude(group);
                if (include == null)
                    return;
                include.Selected = group.Selected ? false : null;
            }
            else
            {
                var mechanic = FindMechanic(group);
                if (mechanic == null)
                    return;
                mechanic.Selected = group.Selected ? false : null;
            }

            Rebuild();
        }

        private void RemoveGroup(ModCollectionGroup group)
        {
            if (group.IsInclude)
            {
                var include = FindInclude(group);
                if (include == null)
                    return;
                Draft(group.ParentCollectionId)?.Includes.Remove(include);
            }
            else
            {
                var mechanic = FindMechanic(group);
                if (mechanic == null)
                    return;
                Draft(group.CollectionId)?.Mechanics.Remove(mechanic);
            }

            _selectedGroupKey = null;
            _selectedModId = null;
            Rebuild();
        }

        private ModCollectionInclude FindInclude(ModCollectionGroup group)
        {
            var parent = Draft(group.ParentCollectionId);
            if (parent?.Includes == null)
                return null;

            return parent.Includes.FirstOrDefault(i =>
                string.Equals(IncludeId(i), group.CollectionId, StringComparison.OrdinalIgnoreCase));
        }

        private ModCollectionMechanic FindMechanic(ModCollectionGroup group)
        {
            var collection = Draft(group.CollectionId);
            if (collection?.Mechanics == null)
                return null;

            return collection.Mechanics.FirstOrDefault(m =>
                string.Equals(m.Id, group.MechanicId, StringComparison.OrdinalIgnoreCase));
        }

        private static string IncludeId(ModCollectionInclude include)
        {
            if (include == null || string.IsNullOrWhiteSpace(include.Ref))
                return null;

            var dependency = ModDependency.Parse(include.Ref);
            return dependency.IsValid ? dependency.ModId : include.Ref.Trim();
        }

        /* ═══════════════ Inspector ═══════════════ */

        private void SelectTab(string tab)
        {
            _tab = tab;
            UpdateInspector();
        }

        private void UpdateInspector()
        {
            _tabInspector.ButtonPressed = _tab == "inspector";
            _tabProblems.ButtonPressed = _tab == "problems";
            _tabPlan.ButtonPressed = _tab == "plan";

            ClearChildren(_detailExtra);
            _apply.Visible = !Embedded && _tab == "plan";

            if (_tab == "problems")
            {
                RenderProblems();
                return;
            }

            if (_tab == "plan")
            {
                RenderPlan();
                return;
            }

            if (_selectedModId != null)
            {
                RenderModInspector(_selectedModId);
                return;
            }

            var group = _selectedGroupKey == null ? null : FindGroup(_selectedGroupKey);
            if (group != null)
            {
                RenderGroupInspector(group);
                return;
            }

            _detailTitle.Text = TranslationServer.Translate("mod_manager/collections_title");
            _detailMeta.Text = string.Empty;
            _detailDescription.Text = TranslationServer.Translate("mod_manager/collection_hint");
            UpdateApplyButton();
        }

        private void RenderGroupInspector(ModCollectionGroup group)
        {
            _detailTitle.Text = group.Name ?? group.CollectionId;

            var metaParts = new List<string>
            {
                group.IsInclude ? TranslationServer.Translate("mod_manager/collection_mechanic") : TranslationServer.Translate("mod_manager/collection_group"),
                roleOrNull(group.Role),
                group.Optional ? TranslationServer.Translate("mod_manager/collection_optional") : null,
                $"{group.Mods.Count} {TranslationServer.Translate("mod_manager/collection_mods")}",
            };
            metaParts.RemoveAll(string.IsNullOrEmpty);
            _detailMeta.Text = string.Join(" · ", metaParts);

            _detailDescription.Text = string.IsNullOrEmpty(group.Description) ? "—" : group.Description;

            var collection = EffectiveCollection(group.CollectionId);
            if (collection != null)
                AddInfoLine(BuildCollectionMeta(collection));

            var actions = UiKit.Row(UiTokens.Space2);
            actions.AddChild(MakeInspectorButton(TranslationServer.Translate("mod_manager/collection_add_mod"),
                () => OpenModPicker(group)));

            if (group.IsInclude)
            {
                actions.AddChild(MakeInspectorButton(TranslationServer.Translate("mod_manager/collection_replace"),
                    () => OpenMechanicPicker(group, true)));
            }

            actions.AddChild(MakeInspectorButton(TranslationServer.Translate("mod_manager/collection_rename"),
                () => OpenRenameDialog(group)));

            if (group.Optional)
            {
                actions.AddChild(MakeInspectorButton(
                    group.Selected ? TranslationServer.Translate("mod_manager/collection_turn_off") : TranslationServer.Translate("mod_manager/collection_turn_on"),
                    () => ToggleGroup(group)));
            }

            actions.AddChild(MakeInspectorButton(TranslationServer.Translate("mod_manager/collection_remove"),
                () => RemoveGroup(group), true));
            _detailExtra.AddChild(actions);
            UpdateApplyButton();
        }

        private void RenderModInspector(string modId)
        {
            var node = _graph?.Nodes?.FirstOrDefault(n => string.Equals(n.ModId, modId, StringComparison.OrdinalIgnoreCase));
            _detailTitle.Text = node?.Title ?? modId;
            _detailMeta.Text = $"{modId} · {CollectionStateNames.Translate(node?.State ?? ModCollectionNodeState.Missing)}";
            _detailDescription.Text = LookupModDescription(modId);

            if (node != null && !string.IsNullOrEmpty(node.RequiredVersion))
                AddInfoLine($"{TranslationServer.Translate("mod_manager/collection_requires")}: {node.RequiredVersion}");
            if (node != null && !string.IsNullOrEmpty(node.InstalledVersion))
                AddInfoLine($"{TranslationServer.Translate("mod_manager/installed_version")}: {node.InstalledVersion}");

            var group = _selectedGroupKey == null ? null : FindGroup(_selectedGroupKey);
            if (group != null)
            {
                AddInfoLine($"{TranslationServer.Translate("mod_manager/collection_in_group")}: {group.Name}");

                var remove = MakeInspectorButton(TranslationServer.Translate("mod_manager/collection_remove_mod"),
                    () => RemoveModFromGroup(group, modId), true);
                _detailExtra.AddChild(remove);
            }

            UpdateApplyButton();
        }

        private void RemoveModFromGroup(ModCollectionGroup group, string modId)
        {
            var refs = group.Mods.Where(m => string.Equals(m.ModId, modId, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var reference in refs)
            {
                var collection = Draft(reference.CollectionId);
                var mechanic = collection?.Mechanics?.FirstOrDefault(m =>
                    string.Equals(m.Id, reference.MechanicId, StringComparison.OrdinalIgnoreCase));
                if (mechanic?.Mods == null)
                    continue;

                mechanic.Mods.RemoveAll(m =>
                    string.Equals(ParseModId(m), modId, StringComparison.OrdinalIgnoreCase));
            }

            _selectedModId = null;
            Rebuild();
        }

        private static string ParseModId(string spec)
        {
            var dependency = ModDependency.Parse(spec);
            return dependency.IsValid ? dependency.ModId : spec?.Trim();
        }

        private void RenderProblems()
        {
            _detailTitle.Text = TranslationServer.Translate("mod_manager/collection_tab_problems");
            _detailMeta.Text = string.Empty;
            _detailDescription.Text = string.Empty;

            if (_graph == null || _graph.Problems.Count == 0)
            {
                _detailExtra.AddChild(UiKit.Label(TranslationServer.Translate("mod_manager/collection_problems_none"), UiTokens.FontBody, UiTokens.SuccessText));
                UpdateApplyButton();
                return;
            }

            foreach (var problem in _graph.Problems)
            {
                var row = UiKit.Column(UiTokens.Space1);
                row.AddChild(UiKit.Label(FormatProblem(problem), UiTokens.FontBodySmall, UiTokens.DangerText));

                if (!string.IsNullOrEmpty(problem.CollectionId) || !string.IsNullOrEmpty(problem.ModId))
                {
                    var target = problem;
                    row.AddChild(MakeInspectorButton(TranslationServer.Translate("mod_manager/collection_show"),
                        () => ShowProblemTarget(target)));
                }

                // Cross link from the mockup: a missing block can be searched
                // in the mods catalog (jumps to the mods tab with the id).
                if (problem.Kind == ModCollectionProblemKind.MissingCollection && !string.IsNullOrEmpty(problem.Ref))
                {
                    var missingId = problem.Ref;
                    row.AddChild(MakeInspectorButton(
                        TranslationServer.Translate("mod_manager/collection_find_in_catalog"),
                        () => OpenManagerRequested?.Invoke(missingId)));
                }

                _detailExtra.AddChild(row);
            }

            UpdateApplyButton();
        }

        private string FormatProblem(ModCollectionProblem problem)
        {
            switch (problem.Kind)
            {
                case ModCollectionProblemKind.MissingCollection:
                    return string.Format(TranslationServer.Translate("mod_manager/collection_problem_missing"), problem.Ref);
                case ModCollectionProblemKind.Cycle:
                    return string.Format(TranslationServer.Translate("mod_manager/collection_problem_cycle"), problem.CollectionId, problem.Ref);
                case ModCollectionProblemKind.RoleConflict:
                    return string.Format(TranslationServer.Translate("mod_manager/collection_problem_role"), problem.Ref);
                case ModCollectionProblemKind.RequirementMissing:
                    return string.Format(TranslationServer.Translate("mod_manager/collection_problem_require"), problem.CollectionId, RerequireCapability(problem.Title));
                default:
                    return problem.Title;
            }
        }

        private static string RerequireCapability(string title)
        {
            var parts = (title ?? string.Empty).Split('\'');
            return parts.Length >= 4 ? parts[3] : title;
        }

        private void ShowProblemTarget(ModCollectionProblem problem)
        {
            if (!string.IsNullOrEmpty(problem.ModId))
            {
                OnModSelected(problem.ModId);
                return;
            }

            OnGroupSelected(problem.GroupKey);
        }

        private void RenderPlan()
        {
            _detailTitle.Text = TranslationServer.Translate("mod_manager/collection_tab_plan");
            _detailMeta.Text = string.Empty;
            _detailDescription.Text = string.Empty;

            if (_plan == null)
                return;

            if (_plan.IsEmpty && !_plan.HasProblems)
            {
                _detailExtra.AddChild(UiKit.Label(TranslationServer.Translate("mod_manager/collection_plan_ready"), UiTokens.FontBody, UiTokens.SuccessText));
                return;
            }

            AddPlanSection("mod_manager/collection_install_count", _plan.Install.Select(i => i.ModId), UiTokens.Info);
            AddPlanSection("mod_manager/collection_enable_count", _plan.Enable, UiTokens.Warning);
            AddPlanSection("mod_manager/collection_missing", _plan.Missing, UiTokens.Danger, findInCatalog: true);
            AddPlanSection("mod_manager/collection_mismatch", _plan.Mismatches, UiTokens.Warning);
            UpdateApplyButton();
        }

        private void AddPlanSection(string captionKey, IEnumerable<string> ids, Color color, bool findInCatalog = false)
        {
            var list = ids?.ToList();
            if (list == null || list.Count == 0)
                return;

            _detailExtra.AddChild(UiKit.Label(
                string.Format(TranslationServer.Translate(captionKey), list.Count), UiTokens.FontBodySmall, color));
            foreach (var id in list)
            {
                if (!findInCatalog)
                {
                    _detailExtra.AddChild(UiKit.Label($"  {id}", UiTokens.FontSmall, UiTokens.Body));
                    continue;
                }

                var row = UiKit.Row(UiTokens.Space2);
                var label = UiKit.Label($"  {id}", UiTokens.FontSmall, UiTokens.Body);
                label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                row.AddChild(label);
                row.AddChild(MakeInspectorButton(
                    TranslationServer.Translate("mod_manager/collection_find_in_catalog"),
                    () => OpenManagerRequested?.Invoke(id)));
                _detailExtra.AddChild(row);
            }
        }

        private void UpdateApplyButton()
        {
            // The mockup rule: "Apply collection" is disabled while problems
            // (missing mods or resolution errors) are on the board.
            var canApply = !_installing
                && _plan != null
                && !_plan.IsEmpty
                && _plan.Missing.Count == 0
                && CountErrors() == 0;
            _apply.Disabled = !canApply;
            if (_buildApply != null)
                _buildApply.Disabled = !canApply;
            UpdateBuildBar();
        }

        /// <summary>
        /// Refreshes the bottom build bar: current game name, install/enable/
        /// problems counters and the hint. Safe during BindSceneNodes — every
        /// field is guarded.
        /// </summary>
        private void UpdateBuildBar()
        {
            if (_buildName == null)
                return;

            _buildName.Text = CurrentCollectionTitle ?? "—";

            var install = _plan?.Install.Count ?? 0;
            var enable = _plan?.Enable.Count ?? 0;
            var problems = CountErrors();
            var blocked = problems > 0
                || (_plan?.Missing.Count ?? 0) > 0
                || (_plan?.Mismatches.Count ?? 0) > 0;

            _buildInstall.Text = install.ToString();
            _buildEnable.Text = enable.ToString();
            _buildEnable.AddThemeColorOverride("font_color", enable > 0 ? UiTokens.Warning : UiTokens.InkBright);
            _buildProblems.Text = problems.ToString();
            _buildProblems.AddThemeColorOverride("font_color", problems > 0 ? UiTokens.DangerText : UiTokens.InkBright);

            _buildHint.Text = TranslationServer.Translate(blocked
                ? "mod_manager/build_hint_problems"
                : "mod_manager/collection_plan_ready");
            _buildHint.AddThemeColorOverride("font_color", blocked ? UiTokens.Warning : UiTokens.SuccessText);
        }

        private void AddInfoLine(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;
            _detailExtra.AddChild(UiKit.Label(text, UiTokens.FontSmall, UiTokens.Muted));
        }

        private string BuildCollectionMeta(ModCollection collection)
        {
            var parts = new List<string> { TranslationServer.Translate(OriginKey(collection.Origin)) };
            if (!string.IsNullOrEmpty(collection.Version))
                parts.Add($"v{collection.Version}");
            if (!string.IsNullOrEmpty(collection.Author))
                parts.Add(collection.Author);
            if (collection.Provides?.Count > 0)
                parts.Add("+" + string.Join(", ", collection.Provides));
            if (collection.Requires?.Count > 0)
                parts.Add("!" + string.Join(", ", collection.Requires));
            return string.Join(" · ", parts);
        }

        private static string OriginKey(ModCollectionOrigin origin) => origin switch
        {
            ModCollectionOrigin.User => "mod_manager/collection_origin_user",
            ModCollectionOrigin.Catalog => "mod_manager/collection_origin_catalog",
            _ => "mod_manager/collection_origin_builtin",
        };

        private static string LookupModDescription(string modId)
        {
            if (ModRegistry.Instance.AllDiscoveredMods.TryGetValue(modId, out var info) && !string.IsNullOrEmpty(info.LocalizedDescription))
                return info.LocalizedDescription;

            var entry = CatalogService.CachedEntries?.FirstOrDefault(e => string.Equals(e.Id, modId, StringComparison.OrdinalIgnoreCase));
            if (entry != null && !string.IsNullOrEmpty(entry.Description))
                return entry.Description;

            return "—";
        }

        private Button MakeInspectorButton(string text, Action action, bool danger = false)
        {
            var button = UiKit.Button(text, danger ? ButtonKind.Danger : ButtonKind.Secondary);
            button.AddThemeFontSizeOverride("font_size", UiTokens.FontSmall);
            button.Pressed += action;
            return button;
        }

        private static string roleOrNull(string role)
            => string.IsNullOrEmpty(role) ? null : role;

        private static void ClearChildren(Node node)
        {
            foreach (var child in node.GetChildren())
            {
                node.RemoveChild(child);
                child.QueueFree();
            }
        }

        private ModCollectionGroup FindGroup(string groupKey)
        {
            if (_graph?.Groups == null || string.IsNullOrEmpty(groupKey))
                return null;

            return _graph.Groups.FirstOrDefault(g => string.Equals(g.Key, groupKey, StringComparison.OrdinalIgnoreCase));
        }

        /* ═══════════════ Dialogs ═══════════════ */

        private void OpenRenameDialog(ModCollectionGroup group)
        {
            var include = group.IsInclude ? FindInclude(group) : null;
            var mechanic = group.IsInclude ? null : FindMechanic(group);
            if (group.IsInclude && include == null)
                return;
            if (!group.IsInclude && mechanic == null)
                return;

            var nameEdit = new LineEdit { Text = group.Name ?? string.Empty };
            var noteEdit = new TextEdit { Text = group.Description ?? string.Empty, CustomMinimumSize = new Vector2(460, 90) };

            var content = UiKit.Column(UiTokens.Space2);
            content.AddChild(UiKit.Label(TranslationServer.Translate("mod_manager/collection_field_name"), UiTokens.FontTiny, UiTokens.Muted));
            content.AddChild(nameEdit);
            content.AddChild(UiKit.Label(TranslationServer.Translate("mod_manager/collection_field_note"), UiTokens.FontTiny, UiTokens.Muted));
            content.AddChild(noteEdit);

            var dialog = new AcceptDialog
            {
                Title = TranslationServer.Translate("mod_manager/collection_rename_title"),
                OkButtonText = TranslationServer.Translate("mod_manager/collection_save"),
                Exclusive = true,
            };
            dialog.AddChild(content);
            dialog.Confirmed += () =>
            {
                ApplyRename(group, nameEdit.Text, noteEdit.Text);
                dialog.QueueFree();
            };
            dialog.Canceled += dialog.QueueFree;

            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(520, 300));
        }

        private void ApplyRename(ModCollectionGroup group, string name, string note)
        {
            if (group.IsInclude)
            {
                var include = FindInclude(group);
                if (include == null)
                    return;
                include.Label = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
                include.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            }
            else
            {
                var mechanic = FindMechanic(group);
                if (mechanic == null)
                    return;
                mechanic.Name = name?.Trim();
                mechanic.Description = note?.Trim();
            }

            Rebuild();
        }

        private void OpenMechanicPicker(ModCollectionGroup replaceTarget, bool replace)
        {
            var content = UiKit.Column(UiTokens.Space2);
            var search = new LineEdit { PlaceholderText = TranslationServer.Translate("mod_manager/collection_search_mechanic") };
            content.AddChild(search);

            var list = new VBoxContainer();
            list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(520, 340), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            scroll.AddChild(list);
            content.AddChild(scroll);

            var dialog = new AcceptDialog
            {
                Title = TranslationServer.Translate(replace
                    ? "mod_manager/collection_pick_replace"
                    : "mod_manager/collection_pick_mechanic"),
                OkButtonText = TranslationServer.Translate("mod_manager/back"),
                Exclusive = true,
            };
            dialog.AddChild(content);

            void Fill()
            {
                ClearChildren(list);
                var query = search.Text?.Trim().ToLowerInvariant() ?? string.Empty;

                if (!replace)
                {
                    list.AddChild(BuildPickerRow(
                        TranslationServer.Translate("mod_manager/collection_empty_mechanic"),
                        TranslationServer.Translate("mod_manager/collection_empty_mechanic_desc"),
                        TranslationServer.Translate("mod_manager/collection_create"),
                        () =>
                        {
                            CreateEmptyMechanic();
                            dialog.QueueFree();
                        }));
                }

                var used = new HashSet<string>(_graph?.Groups.Select(g => g.CollectionId) ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
                var language = TranslationServer.GetLocale();
                var candidates = BuildCollectionIndex().Values
                    .Where(c => c.Kind != ModCollectionKind.Game)
                    .Where(c => !used.Contains(c.Id) || (replace && string.Equals(c.Id, replaceTarget?.CollectionId, StringComparison.OrdinalIgnoreCase)))
                    .Where(c => string.IsNullOrEmpty(query) || c.GetLocalizedName(language).ToLowerInvariant().Contains(query))
                    .OrderBy(c => c.GetLocalizedName(language), StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var candidate in candidates)
                {
                    var target = candidate;
                    list.AddChild(BuildPickerRow(
                        target.GetLocalizedName(language),
                        target.GetLocalizedDescription(language),
                        TranslationServer.Translate(replace
                            ? "mod_manager/collection_replace"
                            : "mod_manager/collection_choose"),
                        () =>
                        {
                            ApplyMechanicChoice(replaceTarget, replace, target);
                            dialog.QueueFree();
                        }));
                }
            }

            search.TextChanged += _ => Fill();
            dialog.Canceled += dialog.QueueFree;
            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(560, 460));
            Fill();
        }

        private Control BuildPickerRow(string title, string description, string buttonText, Action action)
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", UiTokens.Space3);

            var column = UiKit.Column(UiTokens.Space1);
            column.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            column.AddChild(UiKit.Label(title, UiTokens.FontBody, UiTokens.InkBright));
            if (!string.IsNullOrEmpty(description))
                column.AddChild(UiKit.Label(description, UiTokens.FontSmall, UiTokens.Muted));
            row.AddChild(column);

            var button = UiKit.Button(buttonText, ButtonKind.Primary);
            button.AddThemeFontSizeOverride("font_size", UiTokens.FontSmall);
            button.Pressed += action;
            row.AddChild(button);
            return row;
        }

        private void CreateEmptyMechanic()
        {
            var draft = Draft(_currentId);
            if (draft == null)
                return;

            draft.Mechanics ??= new List<ModCollectionMechanic>();
            draft.Mechanics.Add(new ModCollectionMechanic
            {
                Id = UniqueMechanicId(draft),
                Name = TranslationServer.Translate("mod_manager/collection_new_mechanic_name"),
                Role = RoleExtra,
                Description = string.Empty,
                Mods = new List<string>(),
            });

            Rebuild();
        }

        private void ApplyMechanicChoice(ModCollectionGroup replaceTarget, bool replace, ModCollection candidate)
        {
            var draft = Draft(_currentId);
            if (draft == null)
                return;

            if (replace && replaceTarget != null)
            {
                var include = FindInclude(replaceTarget);
                if (include == null)
                    return;

                include.Ref = candidate.Id;
                include.Label = null;
                include.Note = null;
                include.Selected = null;
                Rebuild();
                return;
            }

            draft.Includes ??= new List<ModCollectionInclude>();
            draft.Includes.Add(new ModCollectionInclude
            {
                Ref = candidate.Id,
                Role = GuessRole(candidate),
                Optional = false,
            });

            Rebuild();
        }

        private static readonly string[] KnownRoles = { "systems", "foundation", "battle", "story", "ui", "content", "locale" };

        private static string GuessRole(ModCollection collection)
        {
            foreach (var role in KnownRoles)
            {
                if (collection.Tags != null && collection.Tags.Contains(role, StringComparer.OrdinalIgnoreCase))
                    return role;
            }

            return RoleExtra;
        }

        private static string UniqueMechanicId(ModCollection collection)
        {
            var index = 1;
            while (collection.Mechanics.Any(m => string.Equals(m.Id, $"extra_{index}", StringComparison.OrdinalIgnoreCase)))
                index++;
            return $"extra_{index}";
        }

        private void OpenModPicker(ModCollectionGroup group)
        {
            if (group == null)
                return;

            var child = group.IsInclude ? EffectiveCollection(group.CollectionId) : null;
            var targetSelect = new OptionButton();
            if (group.IsInclude)
            {
                foreach (var mechanic in child?.Mechanics ?? new List<ModCollectionMechanic>())
                    targetSelect.AddItem(mechanic.Name ?? mechanic.Id);
                targetSelect.AddItem(TranslationServer.Translate("mod_manager/collection_new_extra"));
            }

            var content = UiKit.Column(UiTokens.Space2);
            if (group.IsInclude)
            {
                content.AddChild(UiKit.Label(TranslationServer.Translate("mod_manager/collection_where"), UiTokens.FontTiny, UiTokens.Muted));
                content.AddChild(targetSelect);
            }

            var search = new LineEdit { PlaceholderText = TranslationServer.Translate("mod_manager/collection_search_mod") };
            content.AddChild(search);

            var list = new VBoxContainer();
            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(520, 320), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            scroll.AddChild(list);
            content.AddChild(scroll);

            var dialog = new AcceptDialog
            {
                Title = TranslationServer.Translate("mod_manager/collection_add_mod"),
                OkButtonText = TranslationServer.Translate("mod_manager/back"),
                Exclusive = true,
            };
            dialog.AddChild(content);

            void Fill()
            {
                ClearChildren(list);
                var query = search.Text?.Trim().ToLowerInvariant() ?? string.Empty;
                var used = new HashSet<string>(_graph?.Nodes.Select(n => n.ModId) ?? new List<string>(), StringComparer.OrdinalIgnoreCase);

                foreach (var choice in BuildModChoices(used, query))
                {
                    var picked = choice;
                    list.AddChild(BuildPickerRow(
                        picked.Title,
                        picked.Description,
                        TranslationServer.Translate("mod_manager/collection_choose"),
                        () =>
                        {
                            ApplyModChoice(group, picked, targetSelect);
                            dialog.QueueFree();
                        }));
                }
            }

            search.TextChanged += _ => Fill();
            dialog.Canceled += dialog.QueueFree;
            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(560, 440));
            Fill();
        }

        /// <summary>
        /// Mods that can still be added to the current scheme: everything known
        /// to the registry plus catalog entries, minus the ids already resolved,
        /// filtered by the search text. Ordered by title so the picker is stable.
        /// </summary>
        private List<ModChoice> BuildModChoices(HashSet<string> used, string query)
        {
            var choices = new Dictionary<string, ModChoice>(StringComparer.OrdinalIgnoreCase);

            foreach (var info in ModRegistry.Instance.AllDiscoveredMods.Values)
            {
                if (used.Contains(info.Id))
                    continue;

                choices[info.Id] = new ModChoice
                {
                    Id = info.Id,
                    Title = info.DisplayTitle,
                    Description = info.LocalizedDescription,
                };
            }

            foreach (var entry in CatalogService.CachedEntries ?? new List<CatalogExportEntry>())
            {
                if (used.Contains(entry.Id) || choices.ContainsKey(entry.Id))
                    continue;

                choices[entry.Id] = new ModChoice
                {
                    Id = entry.Id,
                    Title = entry.GetLocalizedName(TranslationServer.GetLocale()),
                    Description = entry.GetLocalizedDescription(TranslationServer.GetLocale()),
                };
            }

            return choices.Values
                .Where(c => string.IsNullOrEmpty(query) || c.Title.ToLowerInvariant().Contains(query))
                .OrderBy(c => c.Title, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void ApplyModChoice(ModCollectionGroup group, ModChoice choice, OptionButton targetSelect)
        {
            ModCollection collection;
            ModCollectionMechanic mechanic;

            if (group.IsInclude)
            {
                collection = Draft(group.CollectionId);
                if (collection == null)
                    return;

                collection.Mechanics ??= new List<ModCollectionMechanic>();
                var selected = targetSelect.ItemCount == 0 ? -1 : targetSelect.Selected;
                if (selected >= 0 && selected < collection.Mechanics.Count)
                {
                    mechanic = collection.Mechanics[selected];
                }
                else
                {
                    mechanic = new ModCollectionMechanic
                    {
                        Id = UniqueMechanicId(collection),
                        Name = TranslationServer.Translate("mod_manager/collection_extra_name"),
                        Role = RoleExtra,
                        Description = TranslationServer.Translate("mod_manager/collection_extra_note"),
                        Mods = new List<string>(),
                    };
                    collection.Mechanics.Add(mechanic);
                }
            }
            else
            {
                collection = Draft(group.CollectionId);
                mechanic = collection?.Mechanics?.FirstOrDefault(m =>
                    string.Equals(m.Id, group.MechanicId, StringComparison.OrdinalIgnoreCase));
            }

            if (mechanic == null)
                return;

            mechanic.Mods ??= new List<string>();
            if (!mechanic.Mods.Any(m => string.Equals(ParseModId(m), choice.Id, StringComparison.OrdinalIgnoreCase)))
                mechanic.Mods.Add(choice.Id);

            Rebuild();
        }

        private void OpenSaveDialog()
        {
            var draft = EffectiveCollection(_currentId);
            if (draft == null)
                return;

            var nameEdit = new LineEdit { Text = draft.Name ?? string.Empty };

            var content = UiKit.Column(UiTokens.Space2);
            content.AddChild(UiKit.Label(TranslationServer.Translate("mod_manager/collection_field_name"), UiTokens.FontTiny, UiTokens.Muted));
            content.AddChild(nameEdit);

            var dialog = new AcceptDialog
            {
                Title = TranslationServer.Translate("mod_manager/collection_save"),
                OkButtonText = TranslationServer.Translate("mod_manager/collection_save"),
                Exclusive = true,
            };
            dialog.AddChild(content);
            dialog.Confirmed += () =>
            {
                draft.Name = nameEdit.Text?.Trim();
                SaveDrafts();
                dialog.QueueFree();
            };
            dialog.Canceled += dialog.QueueFree;

            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(480, 200));
        }

        private void SaveDrafts()
        {
            if (_overrides.Count == 0)
            {
                _statusMessage = TranslationServer.Translate("mod_manager/collection_no_changes");
                UpdateBanner();
                return;
            }

            var user = ModCollectionStore.LoadUser();
            foreach (var pair in _overrides)
            {
                var draft = pair.Value;
                draft.Origin = ModCollectionOrigin.User;
                user.RemoveAll(c => string.Equals(c.Id, draft.Id, StringComparison.OrdinalIgnoreCase));
                user.Add(draft);
            }

            ModCollectionStore.SaveUser(user);
            _overrides.Clear();
            _statusMessage = TranslationServer.Translate("mod_manager/collection_save_ok");

            ReloadCollections();
        }

        private void ResetDrafts()
        {
            if (_overrides.Count == 0)
            {
                _statusMessage = TranslationServer.Translate("mod_manager/collection_no_changes");
                UpdateBanner();
                return;
            }

            var dialog = new ConfirmationDialog
            {
                Title = TranslationServer.Translate("mod_manager/collection_reset"),
                DialogText = TranslationServer.Translate("mod_manager/collection_reset_confirm"),
                OkButtonText = TranslationServer.Translate("mod_manager/collection_reset"),
                CancelButtonText = TranslationServer.Translate("mod_manager/cancel"),
                Exclusive = true,
            };

            dialog.Confirmed += () =>
            {
                _overrides.Clear();
                _selectedGroupKey = null;
                _selectedModId = null;
                _statusMessage = null;
                ReloadCollections();
                dialog.QueueFree();
            };
            dialog.Canceled += dialog.QueueFree;

            AddChild(dialog);
            dialog.PopupCentered();
        }

        private void OpenJsonDialog()
        {
            var collection = EffectiveCollection(_currentId);
            var text = collection == null
                ? string.Empty
                : JsonSerializer.Serialize(collection, JsonOptionsProvider.WriteIndentedOptions);

            var view = new TextEdit
            {
                Text = text,
                Editable = false,
                CustomMinimumSize = new Vector2(520, 340),
            };

            var dialog = new AcceptDialog
            {
                Title = TranslationServer.Translate("mod_manager/collection_json"),
                OkButtonText = TranslationServer.Translate("mod_manager/back"),
                Exclusive = true,
            };
            dialog.AddChild(view);
            dialog.Canceled += dialog.QueueFree;
            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(600, 440));
        }

        /* ═══════════════ Actions ═══════════════ */

        private void OnAddMechanicPressed() => OpenMechanicPicker(null, false);

        private void OnClosePressed() => QueueFree();

        private void OnRefreshPressed() => RefreshCatalog();

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
            if (GodotObject.IsInstanceValid(this))
                ReloadCollections();
        }

        private void OnInstallProgress(InstallProgress progress)
        {
            if (!_installing || progress == null)
                return;

            SetBanner(progress.Message);
        }

        private async void OnApplyPressed()
        {
            if (_installing || _plan == null || _plan.IsEmpty
                || _plan.Missing.Count > 0 || CountErrors() > 0)
                return;

            var plan = _plan;
            _installing = true;
            _statusMessage = null;
            SetBanner(string.Format(TranslationServer.Translate("mod_manager/collection_progress"),
                EffectiveCollection(_currentId)?.DisplayTitle ?? _currentId));
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

            Rebuild();
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
    }
}
