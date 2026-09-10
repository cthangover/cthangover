using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Scenes;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Tool
{
    /// <summary>
    /// Full-screen world map overlay toggled by the Map button in the TopView
    /// toolbar. Displays a map background image on a dark semi-transparent
    /// backdrop, with interactive zones overlaid on top.
    ///
    /// Each zone is defined by a <see cref="MapZone"/> entry collected from
    /// <c>mods/&lt;name&gt;/map/*.json</c> files. Zones are rendered as
    /// <see cref="Polygon2D"/> colour washes (semi-transparent highlights) and
    /// <see cref="TextureButton"/> markers placed at normalised coordinates.
    /// Clicking a marker calls <see cref="SceneManager.SwitchScene"/> to
    /// navigate to the zone's linked scene.
    ///
    /// The map closes on Escape, on the dedicated close button, and automatically
    /// when navigating to a new scene through a zone click. UI is built
    /// programmatically in <see cref="OnceConstruct"/> — there is no TSCN subtree
    /// beyond the root Control node.
    /// </summary>
    public partial class MapWidget : Widget
    {
        private const float MapMarginPercent = 0.08f;
        private const float ZonePolygonAlpha = 0.12f;
        private static readonly Color ZonePolygonColor = new(1f, 1f, 1f, ZonePolygonAlpha);
        private static readonly Color OverlayColor = new(0f, 0f, 0f, 0.85f);

        private Control _mapContainer;
        private List<MapZone> _zones;

        public override void _Ready()
        {
            base._Ready();
            MouseFilter = MouseFilterEnum.Stop;
        }

        protected override void OnceConstruct()
        {
            CreateOverlay();
            LoadMapBackground();
            LoadZones();
            CreateZoneNodes();
            CreateCloseButton();
        }

        protected override void ShowConstruct()
        {
            if (_mapContainer != null)
                _mapContainer.Visible = true;
        }

        protected override void HideDestruct()
        {
            if (_mapContainer != null)
                _mapContainer.Visible = false;
        }

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

        private void CreateOverlay()
        {
            var overlay = new ColorRect
            {
                Name = "Overlay",
                Color = OverlayColor,
                MouseFilter = MouseFilterEnum.Ignore,
                AnchorRight = 1f,
                AnchorBottom = 1f,
                GrowHorizontal = GrowDirection.Both,
                GrowVertical = GrowDirection.Both
            };
            AddChild(overlay);
        }

        private void LoadMapBackground()
        {
            var mapTexture = BackgroundFactory.Instance.Get("map");
            if (mapTexture == null)
            {
                GameLogger.Log("MAP", "map texture not found, widget will be empty");
                return;
            }

            _mapContainer = new Control
            {
                Name = "MapContainer",
                MouseFilter = MouseFilterEnum.Ignore
            };

            var screenSize = GetViewportRect().Size;
            var mapSize = mapTexture.GetSize();
            if (mapSize.X <= 0 || mapSize.Y <= 0)
            {
                GameLogger.Log("MAP", $"invalid map texture size: {mapSize}");
                return;
            }

            var mapRatio = mapSize.X / mapSize.Y;
            var availableSize = screenSize * (1f - 2f * MapMarginPercent);
            var screenRatio = availableSize.X / availableSize.Y;

            Vector2 displaySize;
            if (mapRatio > screenRatio)
            {
                displaySize.X = availableSize.X;
                displaySize.Y = availableSize.X / mapRatio;
            }
            else
            {
                displaySize.Y = availableSize.Y;
                displaySize.X = availableSize.Y * mapRatio;
            }

            _mapContainer.Position = (screenSize - displaySize) / 2f;
            _mapContainer.Size = displaySize;

            var textureRect = new TextureRect
            {
                Name = "MapTexture",
                Texture = mapTexture,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.Scale,
                MouseFilter = MouseFilterEnum.Ignore,
                Size = displaySize
            };
            _mapContainer.AddChild(textureRect);
            AddChild(_mapContainer);
        }

        private void LoadZones()
        {
            _zones = new List<MapZone>();
            var opts = JsonOptionsProvider.ReadOptions;

            foreach (var kvp in ModManager.Instance.Mods)
            {
                var modId = kvp.Key;
                var provider = kvp.Value.FileProvider;

                var entries = provider.ListFiles("map");
                foreach (var entry in entries)
                {
                    if (entry.EndsWith("/") || !entry.EndsWith(".json", System.StringComparison.OrdinalIgnoreCase))
                        continue;

                    var jsonText = provider.ReadFileText(entry);
                    if (jsonText == null)
                    {
                        GameLogger.Log("MAP", $"failed to read map json '{modId}/{entry}'", LogLevel.Error);
                        continue;
                    }

                    try
                    {
                        var fileData = JsonSerializer.Deserialize<FileData<MapZone>>(jsonText, opts);
                        if (fileData?.Items != null)
                        {
                            foreach (var zone in fileData.Items)
                            {
                                _zones.Add(zone);
                                GameLogger.Log("MAP", $"loaded zone '{zone.ID}' from '{modId}'");
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        GameLogger.Log("MAP", $"JSON parse failed for '{modId}/{entry}': {ex.Message}", LogLevel.Error);
                    }
                }
            }

            GameLogger.Log("MAP", $"loaded {_zones.Count} zone(s) total");
        }

        private void CreateZoneNodes()
        {
            if (_mapContainer == null || _zones == null)
                return;

            var containerSize = _mapContainer.Size;

            foreach (var zone in _zones)
            {
                CreateZonePolygon(zone, containerSize);
                CreateZoneMarker(zone, containerSize);
            }
        }

        private void CreateZonePolygon(MapZone zone, Vector2 containerSize)
        {
            if (zone.Points == null || zone.Points.Count < 3)
                return;

            var scaledPoints = new Vector2[zone.Points.Count];
            for (var i = 0; i < zone.Points.Count; i++)
                scaledPoints[i] = zone.Points[i] * containerSize;

            var polygon = new Polygon2D
            {
                Polygon = scaledPoints,
                Color = ZonePolygonColor,
                ZIndex = 1
            };
            _mapContainer.AddChild(polygon);
        }

        private void CreateZoneMarker(MapZone zone, Vector2 containerSize)
        {
            if (string.IsNullOrEmpty(zone.Image))
                return;

            var iconTexture = Textures.Resolve(zone.Image);
            if (iconTexture == null)
            {
                GameLogger.Log("MAP", $"marker icon '{zone.Image}' not found for zone '{zone.ID}'", LogLevel.Warning);
                return;
            }

            var iconSize = iconTexture.GetSize();
            var anchorPos = zone.ImagePos * containerSize;

            var btn = new TextureButton
            {
                TextureNormal = iconTexture,
                IgnoreTextureSize = true,
                StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = iconSize,
                Position = anchorPos - iconSize / 2f,
                ZIndex = 2
            };

            var zoneCopy = zone;
            btn.Pressed += () => OnZoneClicked(zoneCopy);
            _mapContainer.AddChild(btn);

            var labelText = TranslationServer.Translate(zone.Title);
            if (!string.IsNullOrEmpty(labelText) && labelText != zone.Title)
            {
                var label = new Label
                {
                    Text = labelText,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    ZIndex = 3,
                    MouseFilter = MouseFilterEnum.Ignore
                };
                label.AddThemeColorOverride("font_color", Colors.White);
                label.AddThemeFontSizeOverride("font_size", 14);

                label.Position = new Vector2(
                    anchorPos.X - 60f,
                    anchorPos.Y + iconSize.Y / 2f + 2f
                );
                label.Size = new Vector2(120f, 20f);

                _mapContainer.AddChild(label);
            }
        }

        private void CreateCloseButton()
        {
            var closeBtn = new TextureButton
            {
                Name = "CloseButton",
                AnchorRight = 1f,
                AnchorBottom = 1f,
                OffsetLeft = -60f,
                OffsetTop = -60f,
                OffsetRight = -10f,
                OffsetBottom = -10f,
                CustomMinimumSize = new Vector2(50f, 50f),
                IgnoreTextureSize = true,
                StretchMode = TextureButton.StretchModeEnum.KeepAspectCentered
            };

            var closeIcon = Textures.Resolve("close");
            if (closeIcon != null)
                closeBtn.TextureNormal = closeIcon;

            closeBtn.Pressed += Hide;
            AddChild(closeBtn);
        }

        private static void OnZoneClicked(MapZone zone)
        {
            if (string.IsNullOrEmpty(zone.Scene))
            {
                GameLogger.Log("MAP", $"zone '{zone.ID}' has no target scene, ignoring click");
                return;
            }

            GameLogger.Log("MAP", $"navigating to scene '{zone.Scene}' from zone '{zone.ID}'");

            var sceneManager = SceneContextNode.FindNode<SceneManager>("SceneManager");
            sceneManager?.SwitchScene(zone.Scene);
        }
    }
}
