using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.Utils;
using Godot;

using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Mods.Interface.Theming;
using Cthangover.Mods.Interface.Tool;
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
        private OptionButton _edgesToggle;
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
        private Button _duplicate;
        private Button _revert;
        private Button _delete;
        private Label _savesInfo;
        private Button _addMod;
        private LineEdit _canvasSearch;
        private Label _zoomLabel;
        private VBoxContainer _versionEditor;
        private OptionButton _versionPolicy;
        private LineEdit _versionValue;
        private Label _versionRule;
        private Label _versionFeedback;
        private ModCollectionModRef _versionReference;
        private bool _fitAfterBuild = true;
        private bool _fitQueued;

        /// <summary>Drafts created by "Duplicate", mapped to the game they were copied from (offer to copy its saves).</summary>
        private readonly Dictionary<string, string> _duplicatedFrom = new(StringComparer.OrdinalIgnoreCase);

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
            _fitAfterBuild = true;
            _selectedGroupKey = null;
            _selectedModId = null;
            // The gallery re-reads the store on every refresh; re-read it here
            // too, so a collection that appeared since this editor was built
            // opens instead of falling back to the first game.
            ReloadCollections();
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

        private void BindGraphiteControls()
        {
            var tools = "Margin/Root/Tools/";
            _addMod = GetNode<Button>(tools + "AddMod");
            _addMod.Text = TranslationServer.Translate("mod_manager/collection_add_mod");
            _addMod.Pressed += OnAddModPressed;
            _canvasSearch = GetNode<LineEdit>(tools + "Search");
            _canvasSearch.PlaceholderText = TranslationServer.Translate("mod_manager/collection_canvas_search");
            _canvasSearch.TextChanged += _canvas.SetSearch;
            _zoomLabel = GetNode<Label>(tools + "Zoom");
            GetNode<Button>(tools + "ZoomOut").Pressed += _canvas.ZoomOut;
            GetNode<Button>(tools + "ZoomIn").Pressed += _canvas.ZoomIn;
            var fit = GetNode<Button>(tools + "Fit");
            fit.Text = TranslationServer.Translate("mod_manager/collection_canvas_fit");
            fit.Pressed += _canvas.FitWidth;
            var arrange = GetNode<Button>(tools + "Arrange");
            arrange.Text = TranslationServer.Translate("mod_manager/collection_canvas_arrange");
            arrange.Pressed += _canvas.ArrangeGroups;
            var focus = GetNode<Button>(tools + "Focus");
            focus.Text = TranslationServer.Translate("mod_manager/collection_canvas_focus");
            focus.Pressed += _canvas.ShowSelection;
            _canvas.SetMiniMap(GetNode<CollectionMiniMap>("Margin/Root/Body/CanvasHost/MiniMap"));

            var path = "Margin/Root/Body/Details/DetailScroll/Detail/VersionEditor";
            _versionEditor = GetNode<VBoxContainer>(path);
            _versionPolicy = GetNode<OptionButton>(path + "/Fields/Policy");
            _versionValue = GetNode<LineEdit>(path + "/Fields/Value");
            _versionRule = GetNode<Label>(path + "/Rule");
            _versionFeedback = GetNode<Label>(path + "/Feedback");
            GetNode<Label>(path + "/Caption").Text = TranslationServer.Translate("mod_manager/collection_version_caption");
            GetNode<Label>(path + "/Hint").Text = TranslationServer.Translate("mod_manager/collection_version_hint");
            var save = GetNode<Button>(path + "/SaveRule");
            save.Text = TranslationServer.Translate("mod_manager/collection_version_edit");
            save.Pressed += OnSaveVersionPressed;
            _versionPolicy.ItemSelected += OnVersionPolicySelected;
            _versionValue.TextChanged += OnVersionValueChanged;
            _versionRule.AddThemeColorOverride("font_color", UiTokens.Accent);
            _versionFeedback.AddThemeColorOverride("font_color", UiTokens.DangerText);
            var policies = new[]
            {
                "mod_manager/collection_version_any", "mod_manager/collection_version_minimum",
                "mod_manager/collection_version_exact", "mod_manager/collection_version_branch",
                "mod_manager/collection_version_maximum", "mod_manager/collection_version_greater",
                "mod_manager/collection_version_less",
            };
            var operators = new[] { "", ">=", "=", "^", "<=", ">", "<" };
            for (var i = 0; i < policies.Length; i++)
            {
                _versionPolicy.AddItem(TranslationServer.Translate(policies[i]));
                _versionPolicy.SetItemMetadata(i, operators[i]);
            }
        }

        private void OnGalleryPressed() => GalleryRequested?.Invoke();

        private void OnBuildManagerPressed() => SelectTab("plan");

        private void OnAddModPressed()
        {
            var group = _selectedGroupKey == null ? null : FindGroup(_selectedGroupKey);
            if (group != null)
                OpenModPicker(group);
        }

        private void OnCanvasViewChanged()
        {
            if (_zoomLabel != null)
                _zoomLabel.Text = Mathf.RoundToInt(_canvas.Zoom * 100f) + "%";
        }

        private void OnCanvasFirstFrame()
        {
            GetTree().ProcessFrame -= OnCanvasFirstFrame;
            _fitQueued = false;
            _fitAfterBuild = false;
            _canvas.FitWidth();
            while (_canvas.Zoom < 0.75f)
                _canvas.ZoomIn();
            _canvas.ShowSelection();
        }

        private void BindVersionRule(ModCollectionGroup group, string modId)
        {
            foreach (var reference in group.Mods)
            {
                if (string.Equals(reference.ModId, modId, StringComparison.OrdinalIgnoreCase))
                {
                    _versionReference = reference;
                    break;
                }
            }
            if (_versionReference == null)
                return;

            var owner = EffectiveCollection(_versionReference.CollectionId);
            foreach (var mechanic in owner?.Mechanics ?? new List<ModCollectionMechanic>())
            {
                if (!string.Equals(mechanic.Id, _versionReference.MechanicId, StringComparison.OrdinalIgnoreCase))
                    continue;
                foreach (var raw in mechanic.Mods ?? new List<string>())
                {
                    var requirement = ModDependency.Parse(raw);
                    if (!requirement.IsValid || !string.Equals(requirement.ModId, modId, StringComparison.OrdinalIgnoreCase))
                        continue;
                    for (var i = 0; i < _versionPolicy.ItemCount; i++)
                    {
                        if (_versionPolicy.GetItemMetadata(i).AsString() == requirement.Operator)
                            _versionPolicy.Selected = i;
                    }
                    _versionValue.Text = requirement.VersionRaw ?? string.Empty;
                    if (requirement.VersionRaw == null
                        && ModRegistry.Instance.AllDiscoveredMods.TryGetValue(modId, out var installed))
                        _versionValue.Text = installed.Version ?? string.Empty;
                    _versionEditor.Visible = true;
                    _versionEditor.GetNode<Label>("Caption").Text = TranslationServer.Translate("mod_manager/collection_version_caption")
                        + " · " + group.Name;
                    OnVersionPolicySelected(_versionPolicy.Selected);
                    return;
                }
            }
        }

        private void OnVersionPolicySelected(long index)
        {
            var op = _versionPolicy.GetItemMetadata((int)index).AsString();
            _versionValue.Editable = op.Length > 0;
            OnVersionValueChanged(_versionValue.Text);
        }

        private void OnVersionValueChanged(string value)
        {
            if (_versionReference == null || _versionPolicy.Selected < 0)
                return;
            var op = _versionPolicy.GetItemMetadata(_versionPolicy.Selected).AsString();
            _versionRule.Text = _versionReference.ModId + op + (op.Length > 0 ? value.Trim() : string.Empty);
            _versionFeedback.Text = op == "^"
                ? TranslationServer.Translate("mod_manager/collection_version_branch_hint") : string.Empty;
            _versionFeedback.AddThemeColorOverride("font_color", UiTokens.Muted);
            foreach (var node in _graph?.Nodes ?? new List<ModCollectionNode>())
            {
                if (node.ModId != _versionReference.ModId || node.State != ModCollectionNodeState.VersionMismatch)
                    continue;
                var requirements = new List<string>();
                foreach (var requirement in node.Requirements)
                {
                    if (!string.IsNullOrEmpty(requirement.Operator))
                        requirements.Add(requirement.Operator + requirement.VersionRaw);
                }
                _versionFeedback.Text = string.Format(TranslationServer.Translate("mod_manager/installed_version"), node.InstalledVersion)
                    + "\n" + string.Format(TranslationServer.Translate("mod_manager/collection_requires"), string.Join(", ", requirements));
                _versionFeedback.AddThemeColorOverride("font_color", UiTokens.DangerText);
                break;
            }
        }

        private void OnSaveVersionPressed()
        {
            if (_versionReference == null || _versionPolicy.Selected < 0)
                return;
            var group = FindGroup(_selectedGroupKey);
            if (group == null)
                return;
            var reference = _versionReference;
            var op = _versionPolicy.GetItemMetadata(_versionPolicy.Selected).AsString();
            var owner = EffectiveCollection(reference.CollectionId);
            var valid = false;
            var changed = false;
            foreach (var mechanic in owner?.Mechanics ?? new List<ModCollectionMechanic>())
            {
                if (!string.Equals(mechanic.Id, reference.MechanicId, StringComparison.OrdinalIgnoreCase))
                    continue;
                var preview = new ModCollectionMechanic { Mods = new List<string>(mechanic.Mods ?? new List<string>()) };
                valid = preview.TrySetModVersion(reference.ModId, op, _versionValue.Text);
                for (var i = 0; i < preview.Mods.Count; i++)
                {
                    if (preview.Mods[i] != mechanic.Mods[i])
                        changed = true;
                }
                break;
            }
            if (!valid)
            {
                _versionFeedback.Text = TranslationServer.Translate("mod_manager/collection_version_invalid");
                _versionFeedback.AddThemeColorOverride("font_color", UiTokens.DangerText);
                return;
            }
            if (!changed)
                return;
            var collection = DraftForEdit(group, reference.CollectionId);
            if (collection == null)
                return;
            foreach (var mechanic in collection.Mechanics ?? new List<ModCollectionMechanic>())
            {
                if (!string.Equals(mechanic.Id, reference.MechanicId, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!mechanic.TrySetModVersion(reference.ModId, op, _versionValue.Text))
                {
                    _versionFeedback.Text = TranslationServer.Translate("mod_manager/collection_version_invalid");
                    _versionFeedback.AddThemeColorOverride("font_color", UiTokens.DangerText);
                    return;
                }
                Rebuild();
                return;
            }
        }

        private void AddModRelationships(string modId)
        {
            AddInfoLine(TranslationServer.Translate("mod_manager/collection_role_hint"));
            foreach (var edge in _graph.Edges)
            {
                if (string.Equals(edge.FromModId, modId, StringComparison.OrdinalIgnoreCase))
                {
                    var consumer = LookupModTitle(edge.ToModId);
                    AddInfoLine(string.Format(TranslationServer.Translate("mod_manager/collection_used_by"), consumer));
                }
            }
            foreach (var collection in _collections)
            {
                if (string.Equals(collection.Id, _currentId, StringComparison.OrdinalIgnoreCase)
                    || collection.Kind != ModCollectionKind.Game)
                    continue;
                var effective = EffectiveCollection(collection.Id);
                foreach (var mechanic in effective?.Mechanics ?? new List<ModCollectionMechanic>())
                {
                    foreach (var raw in mechanic.Mods ?? new List<string>())
                    {
                        if (!string.Equals(ParseModId(raw), modId, StringComparison.OrdinalIgnoreCase))
                            continue;
                        AddInfoLine(string.Format(TranslationServer.Translate("mod_manager/collection_other_role"),
                            CollectionText.Resolve(effective.GetLocalizedName(TranslationServer.GetLocale())),
                            CollectionText.Resolve(mechanic.GetLocalizedName(TranslationServer.GetLocale()))));
                        break;
                    }
                }
            }
        }

        private string LookupModTitle(string modId)
        {
            foreach (var node in _graph.Nodes)
            {
                if (string.Equals(node.ModId, modId, StringComparison.OrdinalIgnoreCase))
                    return node.Title;
            }
            return modId;
        }

        private void BindSceneNodes()
        {
            _close = GetNode<Button>("Margin/Root/Header/Close");
            _refresh = GetNode<Button>("Margin/Root/Header/Refresh");
            _addMechanic = GetNode<Button>("Margin/Root/Header/AddMechanic");
            _newGame = GetNode<Button>("Margin/Root/Header/NewGame");
            _gameSelect = GetNode<OptionButton>("Margin/Root/Header/GameSelect");
            _edgesToggle = GetNode<OptionButton>("Margin/Root/Header/EdgesToggle");
            _pillProblems = GetNode<Button>("Margin/Root/Header/Problems");
            _pillPlan = GetNode<Button>("Margin/Root/Header/Plan");
            _modsCount = GetNode<Label>("Margin/Root/Header/ModsCount");
            _dirty = GetNode<Label>("Margin/Root/Header/Dirty");
            _json = GetNode<Button>("Margin/Root/Header/Json");
            _reset = GetNode<Button>("Margin/Root/Header/Reset");
            _save = GetNode<Button>("Margin/Root/Header/Save");
            _bannerPanel = GetNode<PanelContainer>("Margin/Root/Banner");
            _bannerLabel = GetNode<Label>("Margin/Root/Banner/BannerLabel");
            _canvas = GetNode<CollectionSchemeCanvas>("Margin/Root/Body/CanvasHost/CanvasScroll/SchemeCanvas");

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
            _gameSelect.TooltipText = TranslationServer.Translate("mod_manager/collection_game_tooltip");
            _pillProblems.TooltipText = TranslationServer.Translate("mod_manager/collection_problems_tooltip");
            _pillPlan.TooltipText = TranslationServer.Translate("mod_manager/collection_plan_tooltip");
            _dirty.Text = TranslationServer.Translate("mod_manager/collection_unsaved");
            _json.Text = TranslationServer.Translate("mod_manager/collection_json");
            _reset.Text = TranslationServer.Translate("mod_manager/collection_reset");
            _save.Text = TranslationServer.Translate("mod_manager/collection_save");
            _tabInspector.Text = TranslationServer.Translate("mod_manager/collection_tab_inspector");
            _tabProblems.Text = TranslationServer.Translate("mod_manager/collection_tab_problems");
            _tabPlan.Text = TranslationServer.Translate("mod_manager/collection_tab_plan");
            _json.TooltipText = TranslationServer.Translate("mod_manager/collection_json_tooltip");
            _reset.TooltipText = TranslationServer.Translate("mod_manager/collection_reset_tooltip");
            _apply.Text = TranslationServer.Translate("mod_manager/collection_apply");
            _edgesToggle.AddItem(TranslationServer.Translate("mod_manager/collection_links_selected"));
            _edgesToggle.AddItem(TranslationServer.Translate("mod_manager/collection_links_all"));
            _edgesToggle.AddItem(TranslationServer.Translate("mod_manager/collection_links_none"));
            var orderLegend = GetNode<Label>("Margin/Root/OrderLegend");
            orderLegend.Text = TranslationServer.Translate("mod_manager/collection_order_legend");
            orderLegend.AddThemeColorOverride("font_color", UiTokens.Faint);
            orderLegend.SetMeta(ModHelpPanel.HintTranslationMetadata, "collection_dependencies");

            ModHelpPanel.Attach(_refresh, "refresh");
            ModHelpPanel.Attach(_addMechanic, "collection_add_mechanic");
            ModHelpPanel.Attach(_newGame, "collection_new");
            ModHelpPanel.Attach(_gameSelect, "collection_game");
            ModHelpPanel.Attach(_pillProblems, "collection_problems");
            ModHelpPanel.Attach(_pillPlan, "collection_plan");
            ModHelpPanel.Attach(_json, "collection_json");
            ModHelpPanel.Attach(_reset, "collection_reset");
            ModHelpPanel.Attach(_apply, "collection_apply");
            ModHelpPanel.Attach(_edgesToggle, "collection_links");
            ModHelpPanel.Attach(GetNode<Label>("Margin/Root/OrderLegend"), "load_order");

            var header = _json.GetParent();
            _savesInfo = new Label { VerticalAlignment = VerticalAlignment.Center };
            _savesInfo.AddThemeColorOverride("font_color", UiTokens.Muted);
            _duplicate = new Button { Text = TranslationServer.Translate("mod_manager/collection_duplicate") };
            _revert = new Button { Text = TranslationServer.Translate("mod_manager/collection_revert") };
            _delete = new Button { Text = TranslationServer.Translate("mod_manager/collection_delete") };
            ModHelpPanel.Attach(_delete, "collection_delete");
            foreach (var control in new Control[] { _savesInfo, _duplicate, _revert, _delete })
            {
                header.AddChild(control);
                header.MoveChild(control, _json.GetIndex());
            }
            _duplicate.Pressed += OnDuplicatePressed;
            _revert.Pressed += OnRevertPressed;
            _delete.Pressed += OnDeletePressed;

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

            BindGraphiteControls();
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
            _crumbCollections.Pressed += OnGalleryPressed;
            _crumbCollections.TooltipText = TranslationServer.Translate("mod_manager/gallery_crumb");
            _crumbs.AddChild(_crumbCollections);
            _crumbs.AddChild(UiKit.Label("›", UiTokens.FontBodySmall, UiTokens.Faint));

            GetNode("Margin/Root/Header/GameSelect").Reparent(_crumbs);
            _crumbs.AddChild(UiKit.Label("›", UiTokens.FontBodySmall, UiTokens.Faint));
            _crumbs.AddChild(UiKit.Label(TranslationServer.Translate("mod_manager/collection_crumb_scheme"), UiTokens.FontBodySmall, UiTokens.Body));

            _crumbs.AddChild(UiKit.Spacer());
            GetNode("Margin/Root/Header/ModsCount").Reparent(_crumbs);
            var header = GetNode<HBoxContainer>("Margin/Root/Header");
            _crumbs.Reparent(header);
            header.MoveChild(_crumbs, 0);
            _crumbs.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            GetNode<Control>("Margin/Root/Header/Spacer").Visible = false;
            var tools = GetNode<HBoxContainer>("Margin/Root/Tools");
            _addMechanic.Reparent(tools);
            tools.MoveChild(_addMechanic, 0);
            _edgesToggle.Reparent(tools);
            tools.MoveChild(_edgesToggle, 2);
            _pillProblems.Reparent(tools);
            tools.MoveChild(_pillProblems, 3);
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

            _buildManager = UiKit.Button(TranslationServer.Translate("mod_manager/collection_tab_problems"));
            ModHelpPanel.Attach(_buildManager, "collection_plan");
            _buildManager.Pressed += OnBuildManagerPressed;
            row.AddChild(_buildManager);

            _buildApply = UiKit.Button(TranslationServer.Translate("mod_manager/collection_apply"), ButtonKind.Primary);
            ModHelpPanel.Attach(_buildApply, "collection_apply");
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
            _pillPlan.Visible = false;
        }

        private void SubscribeEvents()
        {
            _close.Pressed += OnClosePressed;
            _refresh.Pressed += OnRefreshPressed;
            _addMechanic.Pressed += OnAddMechanicPressed;
            _newGame.Pressed += OnNewGamePressed;
            _edgesToggle.ItemSelected += _canvas.SetEdgeMode;
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
            _canvas.ViewChanged += OnCanvasViewChanged;

            ModDownloader.OnProgress += OnInstallProgress;
            CatalogService.OnRefreshed += OnCatalogRefreshed;
        }

        private void UnsubscribeEvents()
        {
            ModDownloader.OnProgress -= OnInstallProgress;
            CatalogService.OnRefreshed -= OnCatalogRefreshed;
            if (_fitQueued)
                GetTree().ProcessFrame -= OnCanvasFirstFrame;
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
            foreach (var node in _graph.Nodes)
            {
                if (ModRegistry.Instance.AllDiscoveredMods.TryGetValue(node.ModId, out var info))
                    node.Title = info.DisplayTitle;
            }
            _layout = _currentId == null ? new ModCollectionLayout() : CollectionLayoutStore.Load(_currentId);
            _plan = ModCollectionPlan.Build(_graph, CatalogService.CachedEntries ?? new List<CatalogExportEntry>());

            if (_selectedGroupKey == null && _graph.Groups.Count > 0)
            {
                var initial = _graph.Groups[0];
                foreach (var group in _graph.Groups)
                {
                    if (group.Role == "battle" && group.Selected && group.Mods.Count > 0)
                    {
                        initial = group;
                        break;
                    }
                }
                _selectedGroupKey = initial.Key;
                _selectedModId = initial.Mods.Count > 0 ? initial.Mods[0].ModId : null;
            }

            _canvas.Build(_graph, _layout);
            _canvas.SetSelection(_selectedGroupKey, _selectedModId);
            _canvas.SetEdgeMode(_edgesToggle.Selected);
            if (_fitAfterBuild && !_fitQueued)
            {
                _fitQueued = true;
                GetTree().ProcessFrame += OnCanvasFirstFrame;
            }

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

            var current = EffectiveCollection(_currentId);
            var isGame = current?.Kind == ModCollectionKind.Game;
            _savesInfo.Visible = isGame && !Embedded;
            if (isGame)
                _savesInfo.Text = string.Format(TranslationServer.Translate("mod_manager/collection_saves"), SaveService.ListSaves(_currentId).Count);
            _duplicate.Disabled = current == null;
            _revert.Visible = OriginalCollection(_currentId)?.Origin == ModCollectionOrigin.User
                              && ModCollectionStore.HasShippedVersion(_currentId);
            _delete.Visible = CollectionDeletion.CanDelete(current);

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
            if (errors > 0)
            {
                _pillPlan.Text = $"{TranslationServer.Translate("mod_manager/collection_problems")}: {errors}";
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
            => (_graph?.Problems?.Count ?? 0) + (_plan?.Missing.Count ?? 0) + (_plan?.Mismatches.Count ?? 0);

        private void OnGameSelected(long index)
        {
            var id = _gameSelect.GetItemMetadata((int)index).AsString();
            if (string.IsNullOrEmpty(id) || string.Equals(id, _currentId, StringComparison.OrdinalIgnoreCase))
                return;

            _currentId = id;
            _fitAfterBuild = true;
            _selectedGroupKey = null;
            _selectedModId = null;
            Rebuild();
        }

        private void OnNewGamePressed()
        {
            _fitAfterBuild = true;
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
            _versionEditor.Visible = false;
            _versionReference = null;
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
            AddInfoLine(TranslationServer.Translate("mod_manager/collection_role_hint"));

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
            _detailMeta.Text = $"{modId} · {node?.InstalledVersion ?? "—"} · {CollectionStateNames.Translate(node?.State ?? ModCollectionNodeState.Missing)}";
            _detailDescription.Text = LookupModDescription(modId);

            var group = _selectedGroupKey == null ? null : FindGroup(_selectedGroupKey);
            if (group == null && node != null)
            {
                foreach (var requirement in node.Requirements)
                {
                    if (!string.IsNullOrEmpty(requirement.Operator))
                        AddInfoLine(string.Format(TranslationServer.Translate("mod_manager/collection_requires"), requirement.ToString()));
                }
            }
            if (group != null)
            {
                AddInfoLine($"{TranslationServer.Translate("mod_manager/collection_in_group")}: {group.Name}");
                BindVersionRule(group, modId);
                AddModRelationships(modId);

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
                var collection = DraftForEdit(group, reference.CollectionId);
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

            if (_graph == null || CountErrors() == 0)
            {
                _detailExtra.AddChild(UiKit.Label(TranslationServer.Translate("mod_manager/collection_problems_none"), UiTokens.FontBody, UiTokens.SuccessText));
                UpdateApplyButton();
                return;
            }

            AddPlanSection("mod_manager/collection_missing", _plan?.Missing, UiTokens.DangerText, findInCatalog: true);
            AddPlanSection("mod_manager/collection_mismatch", _plan?.Mismatches, UiTokens.DangerText, findInCatalog: true);

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
            AddPlanSection("mod_manager/collection_mismatch", _plan.Mismatches, UiTokens.Warning, findInCatalog: true);
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
                && !_plan.HasProblems
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
                : _overrides.Count > 0 ? "mod_manager/collection_unsaved" : "mod_manager/collection_plan_ready");
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
                collection = DraftForEdit(group, group.CollectionId);
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
            var duplicates = _overrides.Keys
                .Where(id => _duplicatedFrom.ContainsKey(id) && SaveService.ListSaves(_duplicatedFrom[id]).Count > 0)
                .Select(id => (Copy: id, Source: _duplicatedFrom[id]))
                .ToList();
            _overrides.Clear();
            _duplicatedFrom.Clear();
            // A game's mod set is computed at launch: edits apply on the next start.
            _statusMessage = TranslationServer.Translate("mod_manager/collection_save_ok") + " "
                             + TranslationServer.Translate("mod_manager/collection_next_launch");

            ReloadCollections();
            foreach (var (copy, source) in duplicates)
                OfferSavesCopy(source, copy);
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

        /// <summary>
        /// Shows the current collection as JSON and lets the player edit it:
        /// "Apply" parses the text into the draft (the id must stay the same),
        /// so documents can be fixed or extended beyond what the canvas offers.
        /// </summary>
        private void OpenJsonDialog()
        {
            var collection = EffectiveCollection(_currentId);
            if (collection == null)
                return;

            var view = new CodeEdit
            {
                Text = JsonSerializer.Serialize(collection, JsonOptionsProvider.WriteIndentedOptions),
                Editable = true,
                GuttersDrawLineNumbers = true,
                SyntaxHighlighter = SourceSyntax.CreateJsonHighlighter(),
                IndentUseSpaces = true,
                IndentSize = 2,
                CustomMinimumSize = new Vector2(620, 420),
            };
            view.AddThemeFontOverride("font", ToolWindow.GetMonospaceFont());

            var dialog = new ConfirmationDialog
            {
                Title = TranslationServer.Translate("mod_manager/collection_json"),
                OkButtonText = TranslationServer.Translate("mod_manager/collection_json_apply"),
                CancelButtonText = TranslationServer.Translate("mod_manager/cancel"),
                Exclusive = true,
            };
            dialog.AddChild(view);
            dialog.Confirmed += () =>
            {
                var error = ApplyJson(view.Text);
                dialog.QueueFree();
                if (error != null)
                    ShowNotice(TranslationServer.Translate("mod_manager/collection_json"), error);
            };
            dialog.Canceled += dialog.QueueFree;
            AddChild(dialog);
            dialog.PopupCentered(new Vector2I(700, 520));
        }

        /// <summary>Replaces the current draft with parsed JSON; returns an error message or null.</summary>
        private string ApplyJson(string text)
        {
            ModCollection parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<ModCollection>(text, JsonOptionsProvider.ReadOptions);
            }
            catch (JsonException ex)
            {
                return string.Format(TranslationServer.Translate("mod_manager/collection_json_invalid"), ex.Message);
            }

            if (parsed == null || !string.Equals(parsed.Id, _currentId, StringComparison.OrdinalIgnoreCase))
                return TranslationServer.Translate("mod_manager/collection_json_id_changed");

            parsed.Origin = ModCollectionOrigin.User;
            parsed.Package = OriginalCollection(_currentId)?.Package;
            _overrides[_currentId] = parsed;
            RebuildGameSelect();
            Rebuild();
            return null;
        }

        /// <summary>Copies the current game under a new id (its saves are offered on save).</summary>
        private void OnDuplicatePressed()
        {
            var source = EffectiveCollection(_currentId);
            if (source == null)
                return;

            var copy = CloneCollection(source);
            copy.Id = UniqueGameId();
            copy.Kind = source.Kind;
            copy.Origin = ModCollectionOrigin.User;
            copy.Package = source.Package;
            copy.Name = string.Format(TranslationServer.Translate("mod_manager/collection_copy_name"),
                CollectionText.Resolve(source.GetLocalizedName(TranslationServer.GetLocale())));
            copy.Locale = null;

            _overrides[copy.Id] = copy;
            _duplicatedFrom[copy.Id] = source.Id;
            _currentId = copy.Id;
            _selectedGroupKey = null;
            _selectedModId = null;
            RebuildGameSelect();
            Rebuild();
            OpenSaveDialog();
        }

        private void OfferSavesCopy(string sourceId, string copyId)
        {
            var dialog = new ConfirmationDialog
            {
                Title = TranslationServer.Translate("mod_manager/collection_duplicate"),
                DialogText = string.Format(TranslationServer.Translate("mod_manager/collection_copy_saves"), SaveService.ListSaves(sourceId).Count),
                OkButtonText = TranslationServer.Translate("mod_manager/collection_copy_saves_ok"),
                CancelButtonText = TranslationServer.Translate("mod_manager/cancel"),
                Exclusive = true,
            };
            dialog.Confirmed += () =>
            {
                SaveService.CopyGameSaves(sourceId, copyId);
                dialog.QueueFree();
                UpdateHeader();
            };
            dialog.Canceled += dialog.QueueFree;
            AddChild(dialog);
            dialog.PopupCentered();
        }

        /// <summary>Drops the player's version of a shipped collection after confirmation.</summary>
        private void OnRevertPressed()
        {
            var dialog = new ConfirmationDialog
            {
                Title = TranslationServer.Translate("mod_manager/collection_revert"),
                DialogText = TranslationServer.Translate("mod_manager/collection_revert_confirm"),
                OkButtonText = TranslationServer.Translate("mod_manager/collection_revert"),
                CancelButtonText = TranslationServer.Translate("mod_manager/cancel"),
                Exclusive = true,
            };
            dialog.Confirmed += () =>
            {
                ModCollectionStore.RemoveUser(_currentId);
                _overrides.Remove(_currentId);
                _statusMessage = TranslationServer.Translate("mod_manager/collection_revert_done") + " "
                                 + TranslationServer.Translate("mod_manager/collection_next_launch");
                dialog.QueueFree();
                ReloadCollections();
            };
            dialog.Canceled += dialog.QueueFree;
            AddChild(dialog);
            dialog.PopupCentered();
        }

        /// <summary>
        /// Deletes the current player-made game after confirmation (its unsaved
        /// draft goes too) and switches to the first remaining game.
        /// </summary>
        private void OnDeletePressed()
        {
            var deletedId = _currentId;
            CollectionDeletion.Confirm(this, EffectiveCollection(deletedId), () =>
            {
                _overrides.Remove(deletedId);
                _duplicatedFrom.Remove(deletedId);
                _currentId = null;
                _selectedGroupKey = null;
                _selectedModId = null;
                _statusMessage = TranslationServer.Translate("mod_manager/collection_delete_done");
                ReloadCollections();
            });
        }

        private void ShowNotice(string title, string text)
        {
            var notice = new AcceptDialog { Title = title, DialogText = text, Exclusive = true };
            notice.Confirmed += notice.QueueFree;
            notice.Canceled += notice.QueueFree;
            AddChild(notice);
            notice.PopupCentered();
        }

        /// <summary>
        /// Copy-on-write for shared blocks: an edit inside an included
        /// collection must not change every game that includes it. The first
        /// edit forks the block into "&lt;block&gt;.&lt;game&gt;" (a component
        /// owned by this game) and repoints the include; later edits reuse it.
        /// </summary>
        private ModCollection DraftForEdit(ModCollectionGroup group, string collectionId)
        {
            if (!group.IsInclude || string.Equals(collectionId, _currentId, StringComparison.OrdinalIgnoreCase))
                return Draft(collectionId);

            if (!string.Equals(collectionId, group.CollectionId, StringComparison.OrdinalIgnoreCase))
            {
                _statusMessage = TranslationServer.Translate("mod_manager/collection_nested_readonly");
                UpdateBanner();
                return null;
            }

            var include = FindInclude(group);
            if (include == null)
                return null;

            var forkId = $"{collectionId}.{_currentId}";
            if (EffectiveCollection(forkId) == null)
            {
                var source = EffectiveCollection(collectionId);
                if (source == null)
                    return null;

                var fork = CloneCollection(source);
                fork.Id = forkId;
                fork.Kind = ModCollectionKind.Component;
                fork.Origin = ModCollectionOrigin.User;
                fork.Package = source.Package;
                _overrides[forkId] = fork;
                _statusMessage = string.Format(TranslationServer.Translate("mod_manager/collection_forked"),
                    CollectionText.Resolve(source.GetLocalizedName(TranslationServer.GetLocale())));
                UpdateBanner();
            }

            include.Ref = forkId;
            if (string.Equals(_selectedGroupKey, group.Key, StringComparison.OrdinalIgnoreCase))
                _selectedGroupKey = $"i:{group.ParentCollectionId}:{forkId}";
            return Draft(forkId);
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
                || _plan.HasProblems || CountErrors() > 0)
                return;

            var plan = _plan;
            _installing = true;
            _statusMessage = null;
            SetBanner(string.Format(TranslationServer.Translate("mod_manager/collection_progress"),
                EffectiveCollection(_currentId)?.DisplayTitle ?? _currentId));
            UpdateApplyButton();

            var failed = !await ModCollectionInstaller.ApplyAsync(plan, _sessionInstalled);
            if (!failed)
            {
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
