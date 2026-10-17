using System;
using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Godot;

using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Mods.Interface.Theming;
namespace Mods.ModManager
{
    /// <summary>
    /// Single entry point of the mod UI (the manifest <c>main_menu</c> button
    /// opens this hub instead of two separate screens). A slim header — back
    /// button, title and a two-position segment control "Mods | Collections" —
    /// hosts the child screens: the embedded <see cref="ModManagerScreen"/>
    /// (default tab) and, on the collections tab, the
    /// <see cref="CollectionsGallery"/> plus the scheme editor
    /// <see cref="ModCollectionsScreen"/> as its second level. The selected
    /// tab lives in a static field, so it survives closing and reopening the
    /// hub within one game session.
    /// <para/>
    /// The hub also carries the mockup's cross links: a collection link on a
    /// mod card switches to the collections tab, a "not found" problem in the
    /// editor jumps back to the mods tab with the id filled into the search
    /// box (<see cref="ModManagerScreen.SetSearch"/>), and Escape walks back
    /// editor → gallery → mods tab.
    /// </summary>
    public partial class ModHubScreen : ModWidget
    {
        private const string TabMods = "mods";
        private const string TabCollections = "collections";
        private const string ModsScene = "scenes/mod_manager_screen.tscn";
        private const string EditorScene = "scenes/mod_collections_screen.tscn";

        /// <summary>Active tab, remembered for the whole game session (mockup: "state is remembered").</summary>
        private static string _activeTab = TabMods;

        private Button _back;
        private Label _title;
        private HBoxContainer _segment;
        private Button _tabMods;
        private Button _tabCollections;
        private LineEdit _gallerySearch;
        private Button _galleryRefresh;
        private PanelContainer _bannerPanel;
        private Label _bannerLabel;
        private Control _body;

        private ModManagerScreen _mods;
        private CollectionsGallery _gallery;
        private ModCollectionsScreen _editor;
        private bool _editorOpen;

        /// <summary>Mods installed this session, shared by the gallery and the editor so both stay honest before a restart.</summary>
        private readonly HashSet<string> _sessionInstalled = new(StringComparer.OrdinalIgnoreCase);

        protected override void Construct()
        {
            MouseFilter = MouseFilterEnum.Stop;
            CthangoverTheme.Apply(this);

            BuildHeader();
            BuildBanner();
            BuildBody();

            ShowTab(_activeTab);
        }

        /* ═══════════════ Header and body ═══════════════ */

        private void BuildHeader()
        {
            var root = GetNode<VBoxContainer>("Margin/Root");
            root.AddThemeConstantOverride("separation", UiTokens.Space3);

            var header = UiKit.Row(UiTokens.Space3);
            root.AddChild(header);

            _back = UiKit.Button(TranslationServer.Translate("mod_manager/hub_back_to_menu"));
            _back.Pressed += OnBackPressed;
            header.AddChild(_back);

            _title = UiKit.Label(TranslationServer.Translate("mod_manager/menu_title"), UiTokens.FontLead, UiTokens.Ink);
            _title.Uppercase = true;
            header.AddChild(_title);

            _segment = UiKit.Row(0);
            var segmentPanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            segmentPanel.AddThemeStyleboxOverride("panel", UiKit.Frame(UiTokens.RadiusSm, UiTokens.Well, UiTokens.Line));
            segmentPanel.AddChild(_segment);
            header.AddChild(segmentPanel);

            var group = new ButtonGroup();
            _tabMods = UiKit.Button(TranslationServer.Translate("mod_manager/hub_tab_mods"), ButtonKind.Tab);
            _tabMods.ToggleMode = true;
            _tabMods.ButtonGroup = group;
            _tabMods.Pressed += () => ShowTab(TabMods);
            _segment.AddChild(_tabMods);

            _tabCollections = UiKit.Button(TranslationServer.Translate("mod_manager/hub_tab_collections"), ButtonKind.Tab);
            _tabCollections.ToggleMode = true;
            _tabCollections.ButtonGroup = group;
            _tabCollections.Pressed += () => ShowTab(TabCollections);
            _segment.AddChild(_tabCollections);

            header.AddChild(UiKit.Spacer());

            _gallerySearch = new LineEdit
            {
                PlaceholderText = TranslationServer.Translate("mod_manager/gallery_search_placeholder"),
                CustomMinimumSize = new Vector2(220, 0),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            };
            _gallerySearch.TextChanged += OnGallerySearchChanged;
            header.AddChild(_gallerySearch);

            _galleryRefresh = UiKit.Button(TranslationServer.Translate("mod_manager/refresh"));
            _galleryRefresh.Pressed += OnGalleryRefreshPressed;
            header.AddChild(_galleryRefresh);
        }

        private void BuildBanner()
        {
            var root = GetNode<VBoxContainer>("Margin/Root");
            _bannerPanel = new PanelContainer { Visible = false };
            _bannerPanel.AddThemeStyleboxOverride("panel", ModManagerTheme.MakeBox(
                UiTokens.Alpha(UiTokens.Warning, 0.15f),
                UiTokens.Alpha(UiTokens.Warning, 0.45f),
                UiTokens.RadiusMd, UiTokens.Space4, UiTokens.Space2));
            _bannerLabel = UiKit.Label(string.Empty, UiTokens.FontBodySmall, UiTokens.Warning);
            _bannerLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _bannerPanel.AddChild(_bannerLabel);
            root.AddChild(_bannerPanel);
        }

        private void BuildBody()
        {
            _body = new Control
            {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            GetNode<VBoxContainer>("Margin/Root").AddChild(_body);
        }

        /* ═══════════════ Tabs and levels ═══════════════ */

        private void ShowTab(string tab)
        {
            _activeTab = tab;
            _editorOpen = false;

            if (tab == TabMods)
            {
                EnsureMods();
                _mods.Visible = true;
                if (_gallery != null)
                    _gallery.Visible = false;
                if (_editor != null)
                    _editor.Visible = false;
            }
            else
            {
                EnsureGallery();
                _gallery.Visible = true;
                if (_mods != null)
                    _mods.Visible = false;
                if (_editor != null)
                    _editor.Visible = false;
            }

            UpdateHeader();
        }

        /// <summary>Leaves the editor back to the games gallery (crumbs, back button, Escape).</summary>
        private void ShowGallery()
        {
            _editorOpen = false;
            if (_editor != null)
                _editor.Visible = false;
            EnsureGallery();
            _gallery.Visible = true;
            UpdateHeader();
        }

        /// <summary>
        /// Deep link used by the main menu's game picker ("Configure"): shows
        /// the collections tab with the scheme editor open on the game.
        /// </summary>
        public void OpenCollectionEditor(string collectionId) => OpenEditor(collectionId, false);

        /// <summary>Opens the editor on the second level of the collections tab.</summary>
        private void OpenEditor(string collectionId, bool newGame)
        {
            EnsureGallery();
            EnsureEditor();
            _gallery.Visible = false;
            if (_mods != null)
                _mods.Visible = false;
            _editor.Visible = true;
            _editorOpen = true;

            if (newGame)
                _editor.OpenNewGame();
            else
                _editor.OpenCollection(collectionId);
            UpdateHeader();
        }

        private void UpdateHeader()
        {
            if (_editorOpen && _editor != null)
            {
                _back.Text = TranslationServer.Translate("mod_manager/hub_back_to_gallery");
                _back.TooltipText = _back.Text;
                _title.Text = _editor.CurrentCollectionTitle ?? TranslationServer.Translate("mod_manager/collections_title");
                _segment.Visible = false;
                _gallerySearch.Visible = false;
                _galleryRefresh.Visible = false;
                return;
            }

            _back.Text = TranslationServer.Translate("mod_manager/hub_back_to_menu");
            _back.TooltipText = _back.Text;
            _title.Text = TranslationServer.Translate("mod_manager/menu_title");
            _segment.Visible = true;
            _tabMods.ButtonPressed = _activeTab == TabMods;
            _tabCollections.ButtonPressed = _activeTab == TabCollections;

            var collections = _activeTab == TabCollections;
            _gallerySearch.Visible = collections;
            _galleryRefresh.Visible = collections;
        }

        private void OnBackPressed()
        {
            if (_editorOpen)
                ShowGallery();
            else
                QueueFree();
        }

        /* ═══════════════ Hosted screens ═══════════════ */

        private void EnsureMods()
        {
            if (_mods != null)
                return;

            _mods = TscnScenes.LoadAndBuild(ModsScene) as ModManagerScreen;
            if (_mods == null)
            {
                GameLogger.Log("MOD_HUB", $"Failed to build '{ModsScene}'", LogLevel.Error);
                return;
            }

            _mods.Embedded = true;
            _body.AddChild(_mods);
            _mods.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _mods.EnsureConstructed();
            _mods.CollectionsLinkRequested += () => ShowTab(TabCollections);
        }

        private void EnsureGallery()
        {
            if (_gallery != null)
                return;

            _gallery = new CollectionsGallery();
            _gallery.SetSessionInstalled(_sessionInstalled);
            _gallery.StatusMessage += ShowBanner;
            _gallery.EditSchemeRequested += id => OpenEditor(id, newGame: false);
            _gallery.NewGameRequested += () => OpenEditor(null, newGame: true);
            _body.AddChild(_gallery);
            _gallery.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _gallery.Refresh();
        }

        private void EnsureEditor()
        {
            if (_editor != null)
                return;

            _editor = TscnScenes.LoadAndBuild(EditorScene) as ModCollectionsScreen;
            if (_editor == null)
            {
                GameLogger.Log("MOD_HUB", $"Failed to build '{EditorScene}'", LogLevel.Error);
                return;
            }

            _editor.Embedded = true;
            _editor.SetSessionInstalled(_sessionInstalled);
            _editor.GalleryRequested += ShowGallery;
            _editor.OpenManagerRequested += OnOpenManagerRequested;
            _editor.ViewChanged += UpdateHeader;
            _body.AddChild(_editor);
            _editor.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _editor.EnsureConstructed();
        }

        private void OnOpenManagerRequested(string searchText)
        {
            ShowTab(TabMods);
            _mods?.SetSearch(searchText);
        }

        /* ═══════════════ Gallery header controls ═══════════════ */

        private void OnGallerySearchChanged(string text)
        {
            _gallery?.ApplyFilter(text);
        }

        private async void OnGalleryRefreshPressed()
        {
            if (_galleryRefresh.Disabled)
                return;

            _galleryRefresh.Disabled = true;
            ShowBanner(TranslationServer.Translate("mod_manager/loading"));

            try
            {
                await CatalogService.RefreshAsync(forceRefresh: true);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MOD_HUB", $"Catalog refresh failed: {ex.Message}", LogLevel.Warning);
            }

            if (!GodotObject.IsInstanceValid(this))
                return;

            _galleryRefresh.Disabled = false;
            ShowBanner(null);
        }

        private void ShowBanner(string text)
        {
            if (!GodotObject.IsInstanceValid(_bannerPanel))
                return;

            _bannerPanel.Visible = !string.IsNullOrEmpty(text);
            if (!string.IsNullOrEmpty(text))
                _bannerLabel.Text = text;
        }

        /* ═══════════════ Escape chain ═══════════════ */

        /// <summary>Escape walks back one level: editor → gallery → mods tab.</summary>
        public override void _UnhandledInput(InputEvent @event)
        {
            if (!@event.IsActionPressed("ui_cancel"))
                return;

            if (_editorOpen)
            {
                ShowGallery();
                GetViewport().SetInputAsHandled();
            }
            else if (_activeTab == TabCollections)
            {
                ShowTab(TabMods);
                GetViewport().SetInputAsHandled();
            }
        }
    }
}
