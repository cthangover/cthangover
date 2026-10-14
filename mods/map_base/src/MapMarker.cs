using Cthangover.Core.UI;
using Cthangover.Core.UI.Theming;
using Godot;

using Cthangover.Mods.Interface.Theming;
namespace Cthangover.Mods.Map
{
    /// <summary>
    /// One location marker on the world map: a round emblem with the zone icon,
    /// the localised name pill and an optional state note ("вы здесь" /
    /// "закрыто"). The node structure lives in <c>map_marker.tscn</c>; this
    /// class only binds zone data and repaints state-dependent styles from
    /// <see cref="UiTokens"/>.
    /// <para/>
    /// Visual state priority is current (green — the player's active scene) over
    /// selected (gold) over locked (dimmed, with a drawn padlock) over hover.
    /// The marker never navigates by itself: it reports presses and hover
    /// changes to <see cref="MapWidget"/>, which owns the selected zone, the
    /// location card and the actual scene switch. That keeps a marker reusable
    /// and the map behaviour in one place.
    /// </summary>
    public partial class MapMarker : VBoxContainer
    {
        [Signal] public delegate void ZonePressedEventHandler(string zoneId);
        [Signal] public delegate void ZoneHoverChangedEventHandler(string zoneId, bool hovered);

        private const int EmblemRadius = 23;
        private const float LockBadgeRadius = 9f;

        private Control _emblemWrap;
        private Panel _emblem;
        private TextureRect _glyph;
        private Label _nameLabel;
        private Label _noteLabel;

        private string _zoneId;
        private bool _selected;
        private bool _current;
        private bool _locked;
        private bool _hover;

        /// <summary>Resolves the prefab nodes and subscribes pointer events; the marker never navigates on its own, it only reports to <see cref="MapWidget"/>.</summary>
        public override void _Ready()
        {
            MouseFilter = MouseFilterEnum.Stop;
            ResolveNodes();
            GuiInput += OnGuiInput;
            MouseEntered += OnMouseEntered;
            MouseExited += OnMouseExited;
        }

        /// <summary>Binds a zone to the marker; <paramref name="icon"/> is an already resolved marker texture.</summary>
        public void Setup(MapZone zone, Texture2D icon, bool current, bool locked)
        {
            _zoneId = zone.ID;
            _current = current;
            _locked = locked;
            _selected = false;
            _hover = false;

            if (_glyph != null)
                _glyph.Texture = icon;

            if (_nameLabel != null)
                _nameLabel.Text = TranslationServer.Translate(zone.Title);

            RefreshStyle();
        }

        /// <summary>Re-applies the dynamic zone state after the player moved to another scene.</summary>
        public void ApplyState(bool current, bool locked)
        {
            _current = current;
            _locked = locked;
            RefreshStyle();
        }

        /// <summary>Panel-owned selection: the selected marker gets the golden ring and surface.</summary>
        public void SetSelected(bool selected)
        {
            if (_selected == selected)
                return;
            _selected = selected;
            RefreshStyle();
        }

        /// <summary>
        /// Hover can come from this marker's own pointer events or from the
        /// paired polygon (<see cref="MapZonesLayer.SetHovered"/> mirror); the
        /// method only repaints and never re-emits, so the two layers cannot
        /// loop signals through each other.
        /// </summary>
        public void SetHovered(bool hovered)
        {
            if (_hover == hovered)
                return;
            _hover = hovered;
            RefreshStyle();
        }

        /// <summary>
        /// Draws the padlock badge of a closed zone over the emblem corner.
        /// A drawn badge is used instead of a texture so marker states need no
        /// extra icons from mods and stay sharp at any UI scale.
        /// </summary>
        public override void _Draw()
        {
            if (!_locked || _emblemWrap == null)
                return;

            var wrap = _emblemWrap.GetRect();
            var center = new Vector2(wrap.End.X - 3f, wrap.Position.Y + 3f);
            var outline = UiTokens.BadgeLine(UiTokens.TypeEnemy);

            DrawCircle(center, LockBadgeRadius, UiTokens.Surface0);
            DrawArc(center, LockBadgeRadius, 0f, Mathf.Tau, 28, outline, 1f, true);
            DrawRect(new Rect2(center.X - 4.5f, center.Y - 0.5f, 9f, 6.5f), UiTokens.TypeEnemy, false, 1.2f);
            DrawArc(center + new Vector2(0f, -0.5f), 3.2f, Mathf.Pi, Mathf.Tau, 14, UiTokens.TypeEnemy, 1.4f, true);
        }

        private void ResolveNodes()
        {
            _emblemWrap = GetNodeOrNull<Control>("EmblemWrap");
            _emblem = GetNodeOrNull<Panel>("EmblemWrap/Emblem");
            _glyph = GetNodeOrNull<TextureRect>("EmblemWrap/Emblem/Glyph");
            _nameLabel = GetNodeOrNull<Label>("NameLabel");
            _noteLabel = GetNodeOrNull<Label>("NoteLabel");
        }

        private void RefreshStyle()
        {
            var background = UiTokens.Surface0;
            var border = UiTokens.AccentLine;
            var glyphColor = UiTokens.AccentBright;
            var nameColor = UiTokens.Ink;
            var nameBorder = UiTokens.Line;
            var shadowColor = new Color(0f, 0f, 0f, 0f);
            var shadowSize = 0;

            if (_locked)
            {
                border = UiTokens.LineDisabled;
                glyphColor = UiTokens.Faint;
                nameColor = UiTokens.Faint;
                nameBorder = UiTokens.LineDisabled;
            }

            if (_selected)
            {
                background = UiTokens.SurfaceSelected;
                border = UiTokens.Accent;
                glyphColor = UiTokens.AccentHover;
                nameColor = UiTokens.AccentBright;
                nameBorder = UiTokens.AccentLine;
                shadowColor = UiTokens.Alpha(UiTokens.Accent, 0.35f);
                shadowSize = 6;
            }

            if (_current)
            {
                background = UiTokens.Surface0;
                border = UiTokens.TypePassive;
                glyphColor = UiTokens.SuccessText;
                nameColor = UiTokens.SuccessText;
                nameBorder = UiTokens.BadgeLine(UiTokens.TypePassive);
                shadowColor = UiTokens.SuccessSoft;
                shadowSize = 3;
            }
            else if (_hover && !_selected && !_locked)
            {
                background = UiTokens.Surface1;
                border = UiTokens.AccentLineStrong;
            }

            if (_emblem != null)
            {
                var style = UiKit.Frame(EmblemRadius, background, border);
                if (shadowSize > 0)
                {
                    style.ShadowColor = shadowColor;
                    style.ShadowSize = shadowSize;
                }
                _emblem.AddThemeStyleboxOverride("panel", style);
            }

            if (_glyph != null)
                _glyph.Modulate = glyphColor;

            if (_nameLabel != null)
            {
                var style = UiKit.Frame(UiTokens.RadiusXs, UiTokens.SlotTagBg, nameBorder);
                style.ContentMarginLeft = 8;
                style.ContentMarginRight = 8;
                style.ContentMarginTop = 2;
                style.ContentMarginBottom = 2;
                _nameLabel.AddThemeStyleboxOverride("normal", style);
                _nameLabel.AddThemeColorOverride("font_color", nameColor);
                _nameLabel.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);
            }

            ApplyNote();
            QueueRedraw();
        }

        private void ApplyNote()
        {
            if (_noteLabel == null)
                return;

            _noteLabel.Visible = _current || _locked;
            if (!_noteLabel.Visible)
                return;

            var color = _current ? UiTokens.SuccessText : UiTokens.DangerText;
            var fill = _current ? UiTokens.SuccessSoft : UiTokens.DangerSoft;
            var line = UiTokens.BadgeLine(_current ? UiTokens.TypePassive : UiTokens.TypeEnemy);

            var style = UiKit.Frame(UiTokens.RadiusXs, fill, line);
            style.ContentMarginLeft = 6;
            style.ContentMarginRight = 6;
            style.ContentMarginTop = 1;
            style.ContentMarginBottom = 1;
            _noteLabel.AddThemeStyleboxOverride("normal", style);
            _noteLabel.AddThemeColorOverride("font_color", color);
            _noteLabel.AddThemeFontSizeOverride("font_size", UiTokens.FontMicro);

            _noteLabel.Text = TranslationServer.Translate(_current ? "ui/map/you_are_here" : "ui/map/closed_note").ToString();
            _noteLabel.Uppercase = true;
        }

        private void OnGuiInput(InputEvent @event)
        {
            if (@event is not InputEventMouseButton button || !button.Pressed || button.ButtonIndex != MouseButton.Left)
                return;

            EmitSignal(SignalName.ZonePressed, _zoneId);
            AcceptEvent();
        }

        private void OnMouseEntered()
        {
            if (_hover)
                return;
            _hover = true;
            RefreshStyle();
            EmitSignal(SignalName.ZoneHoverChanged, _zoneId, true);
        }

        private void OnMouseExited()
        {
            if (!_hover)
                return;
            _hover = false;
            RefreshStyle();
            EmitSignal(SignalName.ZoneHoverChanged, _zoneId, false);
        }
    }
}
