using System;
using System.Collections.Generic;
using Cthangover.Core.Events;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.Utils;
using Godot;

using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Mods.Interface.Theming;
namespace Cthangover.Mods.Map
{
    /// <summary>
    /// Full-screen world map overlay toggled by the Map button in the TopView
    /// toolbar. The window structure lives in <c>map_widget.tscn</c>; this
    /// behaviour resolves the nodes, paints them with <see cref="UiTokens"/>,
    /// renders the zones held by <see cref="MapZoneService"/> and drives the
    /// interaction.
    /// <para/>
    /// The widget no longer reads map JSON itself: the zone list and the live
    /// open/hidden state belong to <see cref="MapZoneService"/>, so scenarios
    /// (<c>map.open_zone</c> and friends), save loading and the map editor all
    /// affect the same state, and the map simply redraws from it. All
    /// coordinates are normalised [0..1] against the displayed background
    /// rectangle, which keeps the layout resolution-independent.
    /// <para/>
    /// Two layers compose a zone: the polygon drawn by <see cref="MapZonesLayer"/>
    /// is the generous click target, and the icon produced by the
    /// <c>map_marker.tscn</c> prefab is the readable pinned location. A zone
    /// with a <c>label_pos</c> but no icon is a pure inscription — it is drawn
    /// as text, has no click target and is excluded from the zone counter.
    /// Zones whose runtime state is hidden are absent from both layers
    /// entirely; that is how scenarios keep late-game locations off the map.
    /// A zone whose scene is empty or unknown stays closed — it remains
    /// selectable so the card can explain the block, but its marker is dimmed
    /// and the transition is disabled. The zone matching the active scene is
    /// marked "вы здесь" and cannot be entered again.
    /// <para/>
    /// The map closes on Escape, on the header close button, and automatically
    /// after a successful transition. Zone state is recomputed on every
    /// <see cref="ShowConstruct"/> and the widget also subscribes to
    /// <see cref="MapZoneChangedEvent"/>, so a zone opened by a scenario while
    /// the map is open updates live. <see cref="Reload"/> re-reads the raw zone
    /// data without losing runtime overrides and is used by the map editor.
    /// <see cref="SetPreviewZones"/> turns the widget into a game-like preview
    /// of unsaved editor data: zones and their authoring flags come from the
    /// caller instead of the service, while hover, selection, the location card
    /// and markers behave exactly as in the live map.
    /// </summary>
    public partial class MapWidget : Widget
    {
        private const float MapMarginPercent = 0.08f;
        private const string MarkerScenePath = "scenes/ui/map_marker.tscn";

        private static readonly Color Transparent = new(0f, 0f, 0f, 0f);

        private readonly Dictionary<string, MapMarker> _markers = new();
        private readonly List<Control> _labels = new();

        private ModScenePrefab _markerPrefab;
        private Texture2D _mapTexture;
        private MapZone _selectedZone;
        private IReadOnlyList<MapZone> _previewZones;

        private ColorRect _backdrop;
        private Panel _sheet;
        private TextureRect _mapImage;
        private Panel _frame;
        private MapZonesLayer _zonesLayer;
        private Control _markersLayer;
        private VBoxContainer _hud;
        private Label _hudTitle;
        private Label _hudSub;
        private Button _closeButton;
        private PanelContainer _legend;
        private Panel _legendCurrentDot;
        private Panel _legendAvailableDot;
        private Panel _legendLockedDot;
        private Label _legendCurrentText;
        private Label _legendAvailableText;
        private Label _legendLockedText;
        private PanelContainer _hint;
        private Label _hintLabel;
        private PanelContainer _card;
        private Panel _cardEmblem;
        private TextureRect _cardGlyph;
        private Label _cardName;
        private Label _cardSub;
        private HBoxContainer _cardBadges;
        private Label _cardDesc;
        private Panel _cardRule;
        private Label _cardCaption;
        private Label _cardHint;
        private Label _cardMessage;
        private Button _goButton;
        private CenterContainer _emptyState;
        private Label _emptyTitle;
        private Label _emptyHint;

        /// <summary>Resolves nodes, applies styles and texts, and subscribes to zone changes, close/go buttons and layer signals.</summary>
        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Stop;
            _markerPrefab = new ModScenePrefab(MarkerScenePath);

            ResolveNodes();
            ApplyStaticStyles();
            ApplyStaticTexts();

            if (_emptyState != null)
                MoveChild(_emptyState, GetChildCount() - 1);

            ModEventBus.Subscribe<MapZoneChangedEvent>(OnZoneStateChanged);

            if (_closeButton != null)
                _closeButton.Pressed += OnClosePressed;
            if (_goButton != null)
                _goButton.Pressed += OnGoPressed;
            if (_zonesLayer != null)
            {
                _zonesLayer.ZoneClicked += OnZoneClicked;
                _zonesLayer.ZoneHovered += OnZoneHovered;
            }
        }

        /// <summary>Unsubscribes from <see cref="MapZoneChangedEvent"/> so a closed map does not rebuild pointlessly.</summary>
        public override void _ExitTree()
        {
            ModEventBus.Unsubscribe<MapZoneChangedEvent>(OnZoneStateChanged);
            base._ExitTree();
        }

        protected override void OnceConstruct()
        {
            if (_previewZones == null)
                MapZoneService.Instance.EnsureLoaded();

            LoadMapBackground();
            if (_mapTexture == null)
            {
                UpdateHud();
                return;
            }

            RebuildAll();
        }

        protected override void ShowConstruct()
        {
            ClearSelection();
            LayoutMap();
            RebuildAll();
        }

        protected override void HideDestruct()
        {
            ClearSelection();
        }

        /// <summary>Closes the map on Escape while visible, consuming the input so it does not reach the scene underneath.</summary>
        public override void _Input(InputEvent @event)
        {
            if (!Visible)
                return;

            if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
            {
                Hide();
                GetViewport().SetInputAsHandled();
            }
        }

        /// <summary>
        /// Re-reads the raw map data through <see cref="MapZoneService"/> while
        /// keeping runtime overrides, then rebuilds every layer. Used by the map
        /// editor after saving so the running game reflects the edit without a
        /// restart; safe to call while the map is hidden.
        /// </summary>
        public void Reload()
        {
            MapZoneService.Instance.Reload();
            if (_mapTexture == null)
                LoadMapBackground();
            RebuildAll();
        }

        /// <summary>
        /// Switches the widget into authoring preview mode used by the map
        /// editor: the supplied zone list replaces <see cref="MapZoneService"/>
        /// data, and the initial <see cref="MapZone.Locked"/> /
        /// <see cref="MapZone.Hidden"/> flags are applied directly, without
        /// runtime overrides. Nothing is written back to the service or the
        /// save, so the preview can show unsaved editor state next to the live
        /// game without disturbing either.
        /// </summary>
        public void SetPreviewZones(IReadOnlyList<MapZone> zones)
        {
            _previewZones = zones;
            if (!Visible || _mapTexture == null || _zonesLayer == null)
                return;

            RebuildAll();
        }

        private void OnZoneStateChanged(MapZoneChangedEvent @event)
        {
            if (!Visible || _mapTexture == null)
                return;

            RebuildAll();
        }

        private void RebuildAll()
        {
            if (_zonesLayer == null || _markersLayer == null)
                return;

            ClearOverlay();

            var zones = ZoneSource;
            foreach (var zone in zones)
            {
                if (!IsVisible(zone))
                    continue;

                _zonesLayer.AddZone(zone.ID, zone.Points, IsLocked(zone));
            }

            foreach (var zone in zones)
            {
                if (!IsVisible(zone))
                    continue;

                if (!string.IsNullOrEmpty(zone.Image))
                    BuildMarker(zone);
                else if (zone.LabelPos.HasValue && !string.IsNullOrEmpty(zone.Title))
                    BuildLabel(zone);
            }

            RestoreSelection();
            UpdateHud();
        }

        private void ClearOverlay()
        {
            foreach (var marker in _markers.Values)
            {
                marker.ZonePressed -= OnMarkerPressed;
                marker.ZoneHoverChanged -= OnMarkerHoverChanged;
                marker.QueueFree();
            }
            _markers.Clear();

            foreach (var label in _labels)
                label.QueueFree();
            _labels.Clear();

            _zonesLayer.ClearZones();
        }

        private void BuildMarker(MapZone zone)
        {
            var icon = Textures.Resolve(zone.Image);
            if (icon == null)
            {
                GameLogger.Log("MAP", $"marker icon '{zone.Image}' not found for zone '{zone.ID}'", LogLevel.Warning);
                return;
            }

            var marker = _markerPrefab.Instantiate<MapMarker>();
            if (marker == null)
            {
                GameLogger.Log("MAP", $"failed to instantiate marker for zone '{zone.ID}'", LogLevel.Error);
                return;
            }

            _markersLayer.AddChild(marker);
            marker.Setup(zone, icon, IsCurrent(zone), IsLocked(zone));
            marker.ZonePressed += OnMarkerPressed;
            marker.ZoneHoverChanged += OnMarkerHoverChanged;
            marker.Size = marker.GetCombinedMinimumSize();
            marker.Position = zone.ImagePos * _sheet.Size - marker.Size / 2f;

            _markers[zone.ID] = marker;
        }

        /// <summary>
        /// Creates the free-standing text inscription of a zone that has a
        /// <c>label_pos</c> but no marker icon. Labels use the same pill styling
        /// as marker names but are deliberately non-interactive: they name
        /// regions, they are not locations.
        /// </summary>
        private void BuildLabel(MapZone zone)
        {
            var label = new Label
            {
                Text = Translate(zone.Title),
                MouseFilter = MouseFilterEnum.Ignore,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var style = UiKit.Frame(UiTokens.RadiusXs, UiTokens.SlotTagBg, UiTokens.Line);
            style.ContentMarginLeft = 10;
            style.ContentMarginRight = 10;
            style.ContentMarginTop = 3;
            style.ContentMarginBottom = 3;
            label.AddThemeStyleboxOverride("normal", style);
            label.AddThemeColorOverride("font_color", UiTokens.Ink);
            label.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);

            _markersLayer.AddChild(label);
            label.Size = label.GetCombinedMinimumSize();
            label.Position = zone.LabelPos.Value * _sheet.Size - label.Size / 2f;
            _labels.Add(label);
        }

        /// <summary>
        /// Re-applies the selected-zone highlight after the layers were rebuilt
        /// from scratch, and clears the card when the selected zone became
        /// hidden (or disappeared after a reload).
        /// </summary>
        private void RestoreSelection()
        {
            if (_selectedZone == null)
                return;

            if (!IsVisible(_selectedZone))
            {
                ClearSelection();
                return;
            }

            _zonesLayer.SetSelected(_selectedZone.ID);
            foreach (var pair in _markers)
                pair.Value.SetSelected(pair.Key == _selectedZone.ID);
        }

        private void ClearSelection()
        {
            _selectedZone = null;
            _zonesLayer?.SetSelected(null);

            foreach (var pair in _markers)
                pair.Value.SetSelected(false);

            if (_card != null)
                _card.Visible = false;
        }

        private void ResolveNodes()
        {
            _backdrop = GetNodeOrNull<ColorRect>("Bg");
            _sheet = GetNodeOrNull<Panel>("MapRoot/Sheet");
            _mapImage = GetNodeOrNull<TextureRect>("MapRoot/Sheet/MapImage");
            _frame = GetNodeOrNull<Panel>("MapRoot/Sheet/Frame");
            _zonesLayer = GetNodeOrNull<MapZonesLayer>("MapRoot/Sheet/ZonesLayer");
            _markersLayer = GetNodeOrNull<Control>("MapRoot/Sheet/MarkersLayer");
            _hud = GetNodeOrNull<VBoxContainer>("Hud");
            _hudTitle = GetNodeOrNull<Label>("Hud/Title");
            _hudSub = GetNodeOrNull<Label>("Hud/Sub");
            _closeButton = GetNodeOrNull<Button>("MapRoot/Sheet/CloseButton");
            _legend = GetNodeOrNull<PanelContainer>("Legend");
            _legendCurrentDot = GetNodeOrNull<Panel>("Legend/Items/Current/Dot");
            _legendCurrentText = GetNodeOrNull<Label>("Legend/Items/Current/Text");
            _legendAvailableDot = GetNodeOrNull<Panel>("Legend/Items/Available/Dot");
            _legendAvailableText = GetNodeOrNull<Label>("Legend/Items/Available/Text");
            _legendLockedDot = GetNodeOrNull<Panel>("Legend/Items/Locked/Dot");
            _legendLockedText = GetNodeOrNull<Label>("Legend/Items/Locked/Text");
            _hint = GetNodeOrNull<PanelContainer>("Hint");
            _hintLabel = GetNodeOrNull<Label>("Hint/Label");
            _card = GetNodeOrNull<PanelContainer>("ZoneCard");
            _cardEmblem = GetNodeOrNull<Panel>("ZoneCard/Column/Head/Emblem");
            _cardGlyph = GetNodeOrNull<TextureRect>("ZoneCard/Column/Head/Emblem/Glyph");
            _cardName = GetNodeOrNull<Label>("ZoneCard/Column/Head/Info/Name");
            _cardSub = GetNodeOrNull<Label>("ZoneCard/Column/Head/Info/Sub");
            _cardBadges = GetNodeOrNull<HBoxContainer>("ZoneCard/Column/Badges");
            _cardDesc = GetNodeOrNull<Label>("ZoneCard/Column/Desc");
            _cardRule = GetNodeOrNull<Panel>("ZoneCard/Column/Rule");
            _cardCaption = GetNodeOrNull<Label>("ZoneCard/Column/Caption");
            _cardHint = GetNodeOrNull<Label>("ZoneCard/Column/CardHint");
            _cardMessage = GetNodeOrNull<Label>("ZoneCard/Column/Message");
            _goButton = GetNodeOrNull<Button>("ZoneCard/Column/GoButton");
            _emptyState = GetNodeOrNull<CenterContainer>("EmptyState");
            _emptyTitle = GetNodeOrNull<Label>("EmptyState/EmptyBox/Title");
            _emptyHint = GetNodeOrNull<Label>("EmptyState/EmptyBox/Hint");
        }

        private void ApplyStaticStyles()
        {
            if (_backdrop != null)
                UiKit.ApplyPanelBackdrop(_backdrop);

            if (_sheet != null)
                _sheet.AddThemeStyleboxOverride("panel", UiKit.Frame(UiTokens.RadiusMd, UiTokens.Paper, UiTokens.PaperLine));

            if (_frame != null)
                _frame.AddThemeStyleboxOverride("panel", UiKit.Frame(UiTokens.RadiusXs, Transparent, UiTokens.PaperInset));

            if (_hudTitle != null)
            {
                _hudTitle.AddThemeFontSizeOverride("font_size", UiTokens.FontTitle);
                _hudTitle.AddThemeColorOverride("font_color", UiTokens.InkBright);
            }

            if (_hudSub != null)
            {
                _hudSub.AddThemeFontSizeOverride("font_size", UiTokens.FontBody);
                _hudSub.AddThemeColorOverride("font_color", UiTokens.Muted);
            }

            if (_closeButton != null)
            {
                _closeButton.Text = "✕";
                CthangoverTheme.ApplyIcon(_closeButton);
            }

            if (_goButton != null)
                CthangoverTheme.ApplyPrimary(_goButton);

            StyleLegend();
            StyleHint();
            StyleCard();
            StyleEmptyState();
        }

        private void StyleLegend()
        {
            if (_legend != null)
            {
                var style = UiKit.Frame(UiTokens.RadiusMd, UiTokens.Surface2, UiTokens.Line);
                style.ContentMarginLeft = 14;
                style.ContentMarginRight = 14;
                style.ContentMarginTop = 10;
                style.ContentMarginBottom = 10;
                _legend.AddThemeStyleboxOverride("panel", style);
            }

            StyleLegendItem(_legendCurrentDot, _legendCurrentText, UiTokens.TypePassive, UiTokens.SuccessSoft);
            StyleLegendItem(_legendAvailableDot, _legendAvailableText, UiTokens.Accent, UiTokens.AccentSoft);
            StyleLegendItem(_legendLockedDot, _legendLockedText, Transparent, Transparent);
            if (_legendLockedDot != null)
                _legendLockedDot.AddThemeStyleboxOverride("panel", UiKit.Frame(5, Transparent, UiTokens.LineFaint));
        }

        private static void StyleLegendItem(Panel dot, Label text, Color dotColor, Color ringColor)
        {
            if (dot != null)
            {
                dot.CustomMinimumSize = new Vector2(10f, 10f);
                var style = UiKit.Frame(5, dotColor);
                style.ShadowColor = ringColor;
                style.ShadowSize = 2;
                dot.AddThemeStyleboxOverride("panel", style);
            }

            if (text != null)
            {
                text.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);
                text.AddThemeColorOverride("font_color", UiTokens.Muted);
            }
        }

        private void StyleHint()
        {
            if (_hint != null)
            {
                var style = UiKit.Frame(UiTokens.RadiusSm, UiTokens.SlotTagBg, UiTokens.Line);
                style.ContentMarginLeft = 14;
                style.ContentMarginRight = 14;
                style.ContentMarginTop = 8;
                style.ContentMarginBottom = 8;
                _hint.AddThemeStyleboxOverride("panel", style);
            }

            if (_hintLabel != null)
            {
                _hintLabel.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);
                _hintLabel.AddThemeColorOverride("font_color", UiTokens.Muted);
            }
        }

        private void StyleCard()
        {
            if (_card != null)
            {
                var style = UiKit.Frame(UiTokens.RadiusMd, UiTokens.SurfaceRaised, UiTokens.Line);
                style.ShadowColor = new Color(0f, 0f, 0f, 0.5f);
                style.ShadowSize = 12;
                style.ShadowOffset = new Vector2(0f, 5f);
                style.ContentMarginLeft = 14;
                style.ContentMarginRight = 14;
                style.ContentMarginTop = 14;
                style.ContentMarginBottom = 14;
                _card.AddThemeStyleboxOverride("panel", style);
            }

            if (_cardName != null)
            {
                _cardName.AddThemeFontSizeOverride("font_size", UiTokens.FontH2);
                _cardName.AddThemeColorOverride("font_color", UiTokens.InkBright);
            }

            if (_cardSub != null)
            {
                _cardSub.AddThemeFontSizeOverride("font_size", UiTokens.FontMicro);
                _cardSub.AddThemeColorOverride("font_color", UiTokens.Faint);
            }

            if (_cardDesc != null)
            {
                _cardDesc.AddThemeFontSizeOverride("font_size", UiTokens.FontBody);
                _cardDesc.AddThemeColorOverride("font_color", UiTokens.Body);
            }

            if (_cardRule != null)
                _cardRule.AddThemeStyleboxOverride("panel", UiKit.Frame(0, UiTokens.Line));

            if (_cardCaption != null)
            {
                _cardCaption.AddThemeFontSizeOverride("font_size", UiTokens.FontTiny);
                _cardCaption.AddThemeColorOverride("font_color", UiTokens.AccentDim);
            }

            if (_cardHint != null)
            {
                _cardHint.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);
                _cardHint.AddThemeColorOverride("font_color", UiTokens.Faint);
            }

            if (_cardMessage != null)
            {
                var style = UiKit.Frame(UiTokens.RadiusSm, UiTokens.DangerSoft, UiTokens.Danger);
                style.ContentMarginLeft = 12;
                style.ContentMarginRight = 12;
                style.ContentMarginTop = 8;
                style.ContentMarginBottom = 8;
                _cardMessage.AddThemeStyleboxOverride("normal", style);
                _cardMessage.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);
                _cardMessage.AddThemeColorOverride("font_color", UiTokens.DangerText);
            }
        }

        private void StyleEmptyState()
        {
            if (_emptyTitle != null)
            {
                _emptyTitle.AddThemeFontSizeOverride("font_size", UiTokens.FontBodyLarge);
                _emptyTitle.AddThemeColorOverride("font_color", UiTokens.Faint);
            }

            if (_emptyHint != null)
            {
                _emptyHint.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);
                _emptyHint.AddThemeColorOverride("font_color", UiTokens.Faint);
            }
        }

        private void ApplyStaticTexts()
        {
            if (_hudTitle != null)
                _hudTitle.Text = Translate("ui/map/title").ToUpperInvariant();
            if (_hudSub != null)
                _hudSub.Text = Translate("ui/map/subtitle");
            if (_closeButton != null)
                _closeButton.TooltipText = Translate("ui/map/close");
            if (_legendCurrentText != null)
                _legendCurrentText.Text = Translate("ui/map/legend_current");
            if (_legendAvailableText != null)
                _legendAvailableText.Text = Translate("ui/map/legend_available");
            if (_legendLockedText != null)
                _legendLockedText.Text = Translate("ui/map/legend_locked");
            if (_hintLabel != null)
                _hintLabel.Text = Translate("ui/map/hint");
            if (_cardCaption != null)
                _cardCaption.Text = Translate("ui/map/transition").ToUpperInvariant();
            if (_cardHint != null)
                _cardHint.Text = Translate("ui/map/card_hint");
            if (_cardMessage != null)
                _cardMessage.Text = Translate("ui/map/locked_message");
            if (_emptyTitle != null)
                _emptyTitle.Text = Translate("ui/map/empty");
            if (_emptyHint != null)
                _emptyHint.Text = Translate("ui/map/empty_hint");
        }

        private void LoadMapBackground()
        {
            _mapTexture = BackgroundFactory.Instance.Get("map");
            if (_mapTexture == null)
            {
                GameLogger.Log("MAP", "map texture not found, showing empty state");
                ShowEmptyState(true);
                LayoutMap();
                return;
            }

            if (_mapImage != null)
                _mapImage.Texture = _mapTexture;

            ShowEmptyState(false);
            LayoutMap();
        }

        private void ShowEmptyState(bool empty)
        {
            if (_mapImage != null)
                _mapImage.Visible = !empty;
            if (_zonesLayer != null)
                _zonesLayer.Visible = !empty;
            if (_markersLayer != null)
                _markersLayer.Visible = !empty;
            if (_emptyState != null)
                _emptyState.Visible = empty;
            if (_legend != null)
                _legend.Visible = !empty;
            if (_hint != null)
                _hint.Visible = !empty;
            if (_card != null)
                _card.Visible = false;
        }

        private void LayoutMap()
        {
            if (_sheet == null)
                return;

            var viewportSize = GetViewportRect().Size;
            var available = viewportSize * (1f - 2f * MapMarginPercent);

            Vector2 displaySize;
            if (_mapTexture == null)
            {
                displaySize = available;
            }
            else
            {
                var textureSize = _mapTexture.GetSize();
                if (textureSize.X <= 0f || textureSize.Y <= 0f)
                {
                    GameLogger.Log("MAP", $"invalid map texture size: {textureSize}", LogLevel.Error);
                    _sheet.Position = viewportSize * MapMarginPercent;
                    _sheet.Size = available;
                    return;
                }

                var mapRatio = textureSize.X / textureSize.Y;
                var availableRatio = available.X / available.Y;
                if (mapRatio > availableRatio)
                {
                    displaySize.X = available.X;
                    displaySize.Y = available.X / mapRatio;
                }
                else
                {
                    displaySize.Y = available.Y;
                    displaySize.X = available.Y * mapRatio;
                }
            }

            _sheet.Position = (viewportSize - displaySize) / 2f;
            _sheet.Size = displaySize;
        }

        private void UpdateHud()
        {
            if (_hudSub == null)
                return;

            var count = 0;
            foreach (var zone in ZoneSource)
            {
                if (!IsVisible(zone))
                    continue;
                if (IsLocation(zone))
                    count++;
            }

            _hudSub.Text = $"{Translate("ui/map/subtitle")} · {string.Format(Translate("ui/map/zone_count"), count)}";
        }

        /// <summary>
        /// A "location" is anything the player can see as a place to travel to:
        /// a polygon region or a marker icon. Pure text inscriptions are not
        /// locations and are not counted in the HUD.
        /// </summary>
        private static bool IsLocation(MapZone zone)
        {
            return zone.Points != null && zone.Points.Count >= 3
                || !string.IsNullOrEmpty(zone.Image);
        }

        /// <summary>
        /// The zone list the widget currently renders: the preview list in
        /// authoring preview mode, otherwise the live
        /// <see cref="MapZoneService"/> data.
        /// </summary>
        private IReadOnlyList<MapZone> ZoneSource => _previewZones ?? MapZoneService.Instance.Zones;

        /// <summary>
        /// Effective visibility of a single zone: the authoring flag in preview
        /// mode (no runtime overrides), otherwise the service state.
        /// </summary>
        private bool IsVisible(MapZone zone)
        {
            return _previewZones == null
                ? MapZoneService.Instance.IsVisible(zone.ID)
                : !zone.Hidden;
        }

        /// <summary>
        /// Effective open state of a single zone: the authoring flag in preview
        /// mode, otherwise the service state. Whether the zone is actually
        /// enterable still depends on its scene resolving — see
        /// <see cref="IsLocked"/>.
        /// </summary>
        private bool IsOpen(MapZone zone)
        {
            return _previewZones == null
                ? MapZoneService.Instance.IsOpen(zone.ID)
                : !zone.Locked;
        }

        /// <summary>Turns a zone ID coming from a marker or polygon signal back into the rendered zone.</summary>
        private MapZone FindZone(string id)
        {
            if (_previewZones == null)
                return MapZoneService.Instance.Get(id);

            foreach (var zone in _previewZones)
            {
                if (string.Equals(zone.ID, id, StringComparison.OrdinalIgnoreCase))
                    return zone;
            }

            return null;
        }

        private static bool IsCurrent(MapZone zone)
        {
            var current = SceneManager.GetCurrentSceneName();
            if (string.IsNullOrEmpty(current))
                return false;

            return string.Equals(zone.Scene, current, StringComparison.OrdinalIgnoreCase)
                || string.Equals(zone.ID, current, StringComparison.OrdinalIgnoreCase);
        }

        private bool IsLocked(MapZone zone)
        {
            return MapZoneRules.IsLocked(zone, IsOpen(zone));
        }

        private void OnZoneClicked(string zoneId)
        {
            SelectZone(FindZone(zoneId));
        }

        private void OnMarkerPressed(string zoneId)
        {
            SelectZone(FindZone(zoneId));
        }

        private void OnZoneHovered(string zoneId, bool hovered)
        {
            if (_markers.TryGetValue(zoneId, out var marker))
                marker.SetHovered(hovered);
        }

        private void OnMarkerHoverChanged(string zoneId, bool hovered)
        {
            _zonesLayer?.SetHovered(zoneId, hovered);
        }

        private void SelectZone(MapZone zone)
        {
            if (zone == null)
                return;

            _selectedZone = zone;
            _zonesLayer?.SetSelected(zone.ID);
            foreach (var pair in _markers)
                pair.Value.SetSelected(pair.Key == zone.ID);

            RefreshCard(zone);
        }

        private void RefreshCard(MapZone zone)
        {
            if (_card == null)
                return;

            var current = IsCurrent(zone);
            var locked = IsLocked(zone);

            if (_cardName != null)
                _cardName.Text = Translate(zone.Title);
            if (_cardSub != null)
                _cardSub.Text = string.IsNullOrEmpty(zone.Scene) ? zone.ID : $"scene: {zone.Scene}";
            if (_cardDesc != null)
            {
                _cardDesc.Visible = !string.IsNullOrEmpty(zone.Description);
                _cardDesc.Text = _cardDesc.Visible ? Translate(zone.Description) : string.Empty;
            }
            if (_cardMessage != null)
                _cardMessage.Visible = locked && !current;

            RebuildCardBadges(current, locked);
            ApplyCardEmblem(zone, current, locked);

            if (_goButton != null)
            {
                _goButton.Disabled = current || locked;
                _goButton.Text = current ? Translate("ui/map/here") : Translate("ui/map/go");
            }

            _card.Visible = true;
        }

        private void RebuildCardBadges(bool current, bool locked)
        {
            if (_cardBadges == null)
                return;

            ClearChildren(_cardBadges);

            if (current)
                _cardBadges.AddChild(UiKit.Badge(Translate("ui/map/current"), UiTokens.TypePassive));
            else if (locked)
                _cardBadges.AddChild(UiKit.Badge(Translate("ui/map/locked"), UiTokens.TypeEnemy));
            else
                _cardBadges.AddChild(UiKit.Badge(Translate("ui/map/available"), UiTokens.Accent));
        }

        private void ApplyCardEmblem(MapZone zone, bool current, bool locked)
        {
            if (_cardEmblem == null)
                return;

            var accent = current ? UiTokens.TypePassive : locked ? UiTokens.TypeEnemy : UiTokens.Accent;
            _cardEmblem.AddThemeStyleboxOverride("panel", UiKit.Frame(29, UiTokens.Surface2, UiTokens.BadgeLine(accent)));

            if (_cardGlyph == null)
                return;

            _cardGlyph.Texture = string.IsNullOrEmpty(zone.Image) ? null : Textures.Resolve(zone.Image);
            _cardGlyph.Modulate = current ? UiTokens.SuccessText : locked ? UiTokens.Faint : UiTokens.AccentBright;
        }

        private void OnGoPressed()
        {
            var zone = _selectedZone;
            if (zone == null || string.IsNullOrEmpty(zone.Scene) || IsCurrent(zone) || IsLocked(zone))
                return;

            var sceneManager = SceneContextNode.FindNode<SceneManager>("SceneManager");
            if (sceneManager == null)
            {
                GameLogger.Log("MAP", "SceneManager not found, cannot switch scene", LogLevel.Error);
                return;
            }

            GameLogger.Log("MAP", $"navigating to scene '{zone.Scene}' from zone '{zone.ID}'");
            sceneManager.SwitchScene(zone.Scene);
            Hide();
        }

        private void OnClosePressed()
        {
            Hide();
        }

        private static void ClearChildren(Node parent)
        {
            if (parent == null)
                return;

            foreach (var child in parent.GetChildren())
                child.QueueFree();
        }

        private static string Translate(string key)
        {
            return string.IsNullOrEmpty(key) ? string.Empty : TranslationServer.Translate(key).ToString();
        }
    }
}
