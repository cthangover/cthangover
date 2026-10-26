using System;
using Cthangover.Core.Mods;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Theming;
using Godot;

using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Mods.Interface.Theming;
namespace Mods.ModManager
{
    /// <summary>
    /// One contour of the collection scheme: the Godot counterpart of the
    /// mockup's <c>.ce-group</c>. Renders a movable and resizable frame with the
    /// mechanic role, name, state badge and description, hosts the draggable mod
    /// chips on an absolute layer and draws the faint watermark label on the
    /// background.
    /// <para/>
    /// The card owns no data beyond the resolved <see cref="ModCollectionGroup"/>
    /// it displays: the header carries only the state pill and the optional
    /// contour's on/off toggle (raising <see cref="Command"/> with "toggle"),
    /// while editing commands live in the screen's inspector panel. Group and
    /// chip coordinates are reported
    /// through <see cref="Moved"/>, <see cref="Resized"/> and
    /// <see cref="DragFinished"/> so the canvas can persist them through
    /// <see cref="CollectionLayoutStore"/>.
    /// </summary>
    public partial class CollectionGroupCard : ModWidget
    {
        /// <summary>Minimum frame width enforced while resizing.</summary>
        public const int MinWidth = 300;

        /// <summary>Minimum frame height enforced while resizing.</summary>
        public const int MinHeight = 140;

        /// <summary>Stable group key matching <see cref="ModCollectionGroup.Key"/>.</summary>
        public string GroupKey { get; private set; }

        /// <summary>Resolved group shown by this card.</summary>
        public ModCollectionGroup Group { get; private set; }

        /// <summary>Layer the canvas adds mod chips to; absolute coordinates start at the frame's top-left.</summary>
        public Control ModsLayer { get; private set; }

        /// <summary>
        /// Card-local Y where the free chip area begins: the bottom of the
        /// header and description block plus a small gap. <see cref="CollectionModChip"/>
        /// clamps vertical drags to this line, so a chip can never be parked on
        /// top of the group title, its header buttons or its description. The
        /// canvas keeps its own default for freshly packed slots.
        /// </summary>
        public float ModsTop
        {
            get
            {
                EnsureConstructed();
                var block = _content.Position.Y + _desc.Position.Y + _desc.Size.Y;
                return Mathf.Max(0f, block + UiTokens.Space2);
            }
        }

        /// <summary>Raised on left click anywhere on the card that is not a chip or button.</summary>
        public event Action<string> Selected;

        /// <summary>Raised by the header buttons with a command id (see class summary).</summary>
        public event Action<string, string> Command;

        /// <summary>Raised while dragging the header with the desired canvas position.</summary>
        public event Action<string, Vector2> Moved;

        /// <summary>Raised while dragging the resize corner with the desired frame size.</summary>
        public event Action<string, Vector2> Resized;

        /// <summary>Raised once a header drag or resize ends, so layout can be flushed.</summary>
        public event Action DragFinished;

        private PanelContainer _panel;
        private VBoxContainer _content;
        private HBoxContainer _header;
        private Label _role;
        private Label _name;
        private Label _desc;
        private Label _watermark;
        private PanelContainer _stateChip;
        private Label _stateLabel;
        private CheckButton _optional;
        private CollectionResizeHandle _resize;
        private StyleBoxFlat _style;
        private Color _baseBorder = UiTokens.LineCard;

        private bool _dragging;
        private bool _resizing;
        private bool _moved;
        private bool _updatingOptional;

        /// <summary>
        /// Binds the card to a resolved group. Must be called after the card is
        /// added to the tree; safe to call again to refresh texts and states.
        /// </summary>
        public void Init(ModCollectionGroup group, string stateLabel, Color stateColor, bool selected)
        {
            EnsureConstructed();

            Group = group;
            GroupKey = group.Key;
            _role.Text = string.IsNullOrEmpty(group.Role) ? string.Empty : group.Role.ToUpperInvariant();
            _name.Text = group.Name;
            _desc.Text = group.Description ?? string.Empty;
            _watermark.Text = group.Name ?? string.Empty;
            _stateLabel.Text = stateLabel;
            _stateLabel.AddThemeColorOverride("font_color", stateColor);
            _stateChip.AddThemeStyleboxOverride("panel", UiKit.Frame(UiTokens.RadiusXs, UiTokens.BadgeFill(stateColor), UiTokens.BadgeLine(stateColor)));

            if (_optional != null)
            {
                _updatingOptional = true;
                _optional.ButtonPressed = group.Selected;
                _updatingOptional = false;
                _optional.Visible = group.Optional;
            }

            _header.TooltipText = group.Description ?? string.Empty;
            Modulate = new Color(1, 1, 1, group.Selected ? 1f : 0.55f);
            SetSelected(selected);
        }

        /// <summary>Moves and sizes the frame in canvas coordinates.</summary>
        public void SetFrame(Rect2 rect)
        {
            Position = rect.Position;
            Size = rect.Size;
            _watermark.Size = new Vector2(Mathf.Max(0f, rect.Size.X - 24f), 52f);
            _watermark.Position = new Vector2(8f, Mathf.Max(0f, rect.Size.Y - 56f));
            _resize.Position = new Vector2(Mathf.Max(0f, rect.Size.X - CollectionResizeHandle.HandleSize), Mathf.Max(0f, rect.Size.Y - CollectionResizeHandle.HandleSize));
        }

        /// <summary>Highlights the frame as the current canvas/inspector selection.</summary>
        public void SetSelected(bool selected)
        {
            EnsureConstructed();
            _style.BorderColor = selected ? UiTokens.Accent : _baseBorder;
            _style.BorderWidthTop = 1;
            _style.BorderWidthRight = 1;
            _style.BorderWidthBottom = 1;
            _style.BorderWidthLeft = 1;
        }

        /// <summary>Updates only the state pill, used when the plan changes the availability.</summary>
        public void SetState(string stateLabel, Color stateColor)
        {
            EnsureConstructed();
            _stateLabel.Text = stateLabel;
            _stateLabel.AddThemeColorOverride("font_color", stateColor);
            _stateChip.AddThemeStyleboxOverride("panel", UiKit.Frame(UiTokens.RadiusXs, UiTokens.BadgeFill(stateColor), UiTokens.BadgeLine(stateColor)));
        }

        protected override void Construct()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            _baseBorder = UiTokens.LineCard;

            _style = UiKit.Frame(UiTokens.RadiusLg, new Color(UiTokens.Surface1, 0.92f), _baseBorder);
            _panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
            _panel.AddThemeStyleboxOverride("panel", _style);
            _panel.GuiInput += OnPanelGuiInput;
            AddChild(_panel);
            _panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            ModsLayer = new Control { MouseFilter = MouseFilterEnum.Ignore };
            AddChild(ModsLayer);
            ModsLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            var contentHost = new Control { MouseFilter = MouseFilterEnum.Ignore };
            AddChild(contentHost);
            contentHost.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            var margin = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
            margin.AddThemeConstantOverride("margin_left", UiTokens.Space6);
            margin.AddThemeConstantOverride("margin_right", UiTokens.Space6);
            margin.AddThemeConstantOverride("margin_top", UiTokens.Space5);
            margin.AddThemeConstantOverride("margin_bottom", UiTokens.Space5);
            contentHost.AddChild(margin);
            margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            _content = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            _content.AddThemeConstantOverride("separation", 6);
            margin.AddChild(_content);

            _header = new HBoxContainer { MouseFilter = MouseFilterEnum.Stop };
            _header.AddThemeConstantOverride("separation", UiTokens.Space3);
            _header.GuiInput += OnHeaderGuiInput;
            _content.AddChild(_header);

            _role = UiKit.Label(string.Empty, UiTokens.FontMicro, UiTokens.AccentDim);
            _role.Uppercase = true;
            _role.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            _header.AddChild(_role);

            _name = UiKit.Label(string.Empty, UiTokens.FontLead, UiTokens.InkBright);
            _name.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            _name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _name.ClipText = true;
            _header.AddChild(_name);

            _stateLabel = UiKit.Label(string.Empty, UiTokens.FontMicro, UiTokens.Success);
            _stateLabel.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            _stateChip = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
            _stateChip.AddChild(_stateLabel);
            _header.AddChild(_stateChip);

            // Commands (add mod, rename, replace, remove) live only in the
            // right-hand inspector — the header stays a compact title row with
            // the state pill and, for optional contours, the on/off toggle.

            _optional = new CheckButton { Visible = false };
            _optional.Toggled += OnOptionalToggled;
            _header.AddChild(_optional);

            _desc = UiKit.Label(string.Empty, UiTokens.FontSmall, UiTokens.Muted);
            _desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _desc.MaxLinesVisible = 1;
            _content.AddChild(_desc);

            _watermark = UiKit.Label(string.Empty, 42, new Color(1f, 1f, 1f, 0.04f));
            _watermark.Uppercase = true;
            _watermark.ClipText = true;
            _watermark.HorizontalAlignment = HorizontalAlignment.Right;
            AddChild(_watermark);

            _resize = new CollectionResizeHandle();
            _resize.GuiInput += OnResizeGuiInput;
            AddChild(_resize);
            _resize.EnsureConstructed();
        }

        private void OnOptionalToggled(bool pressed)
        {
            if (_updatingOptional)
                return;

            Command?.Invoke(GroupKey, "toggle");
        }

        private void OnPanelGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left && button.Pressed)
            {
                Selected?.Invoke(GroupKey);
                AcceptEvent();
            }
        }

        private void OnHeaderGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left)
            {
                if (button.Pressed)
                {
                    _dragging = true;
                    _moved = false;
                    Selected?.Invoke(GroupKey);
                }
                else
                {
                    _dragging = false;
                    if (_moved)
                        DragFinished?.Invoke();
                }

                AcceptEvent();
                return;
            }

            if (@event is InputEventMouseMotion motion && _dragging)
            {
                var desired = Position + motion.Relative;
                var clamped = new Vector2(Mathf.Max(0f, desired.X), Mathf.Max(0f, desired.Y));
                if (clamped.DistanceTo(Position) > 0.5f)
                    _moved = true;

                Position = clamped;
                Moved?.Invoke(GroupKey, clamped);
                AcceptEvent();
            }
        }

        private void OnResizeGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left)
            {
                if (button.Pressed)
                {
                    _resizing = true;
                    _moved = false;
                }
                else
                {
                    _resizing = false;
                    if (_moved)
                        DragFinished?.Invoke();
                }

                AcceptEvent();
                return;
            }

            if (@event is InputEventMouseMotion motion && _resizing)
            {
                var size = new Vector2(
                    Mathf.Max(MinWidth, Size.X + motion.Relative.X),
                    Mathf.Max(MinHeight, Size.Y + motion.Relative.Y));
                if (size.DistanceTo(Size) > 0.5f)
                    _moved = true;

                SetFrame(new Rect2(Position, size));
                Resized?.Invoke(GroupKey, size);
                AcceptEvent();
            }
        }

        protected override void Destruct()
        {
            _optional.Toggled -= OnOptionalToggled;
        }
    }
}
