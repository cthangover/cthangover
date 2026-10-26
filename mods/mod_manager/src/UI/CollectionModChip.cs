using System;
using Cthangover.Core.UI;
using Cthangover.Core.UI.Theming;
using Godot;

using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Mods.Interface.Theming;
namespace Mods.ModManager
{
    /// <summary>
    /// Draggable chip of one mod inside a scheme group, the Godot counterpart of
    /// the mockup's <c>.ce-mod</c>. Built fully in code and used through the
    /// <see cref="ModWidget"/> contract: the canvas adds it to the mods layer
    /// first and only then calls <see cref="Init"/>, which builds the visuals
    /// under a valid tree.
    /// <para/>
    /// The chip is click-selectable and freely draggable inside its group: the
    /// drag pattern keeps the pointer over the panel because the control follows
    /// the mouse by the same delta (the same trick the map editor handles use).
    /// It owns no data — the canvas persists the resulting position into
    /// <see cref="Cthangover.Core.Mods.ModCollectionLayout"/> and decides what a
    /// click means.
    /// </summary>
    public partial class CollectionModChip : ModWidget
    {
        /// <summary>Fixed chip width in canvas pixels; the canvas layout math depends on it.</summary>
        public const int ChipWidth = 210;

        /// <summary>Fixed chip height in canvas pixels.</summary>
        public const int ChipHeight = 94;

        /// <summary>Canonical mod id this chip represents.</summary>
        public string ModId { get; private set; }

        /// <summary>The displayed occurrence's group; a mod may have another chip in another group.</summary>
        public string GroupKey { get; private set; }

        /// <summary>Raised on left click; the canvas turns it into a selection.</summary>
        public event Action<CollectionModChip> Selected;

        /// <summary>Raised while dragging with the desired position inside the group's mods layer.</summary>
        public event Action<CollectionModChip, Vector2> Moved;

        /// <summary>Raised when a drag ends, after the last <see cref="Moved"/>.</summary>
        public event Action DragFinished;

        private PanelContainer _panel;
        private Label _role;
        private Label _name;
        private Label _meta;
        private ColorRect _dot;
        private StyleBoxFlat _style;
        private Color _stateColor = UiTokens.Success;

        private bool _dragging;
        private bool _moved;

        /// <summary>
        /// Binds the chip to a resolved mod and paints it. Must be called after
        /// the chip is added to the tree; safe to call again to refresh the
        /// status without rebuilding the node.
        /// </summary>
        public void Init(string groupKey, string modId, string role, string title, string version, string requirement, string stateLabel, Color stateColor, string tooltip)
        {
            EnsureConstructed();

            ModId = modId;
            GroupKey = groupKey;
            _stateColor = stateColor;
            _role.Text = string.IsNullOrEmpty(role) ? string.Empty : role.ToUpperInvariant();
            _name.Text = title;
            _meta.Text = string.IsNullOrEmpty(version) ? stateLabel
                : version + (string.IsNullOrEmpty(requirement) ? string.Empty : " · " + requirement);
            _dot.Color = stateColor;
            _style.BorderColor = stateColor == UiTokens.Danger || stateColor == UiTokens.DangerText || stateColor == UiTokens.Warning
                ? stateColor : UiTokens.LineCard;
            _panel.TooltipText = tooltip ?? string.Empty;
        }

        /// <summary>Highlights the chip as the current canvas/inspector selection.</summary>
        public void SetSelected(bool selected)
        {
            EnsureConstructed();
            _style.BorderColor = selected ? UiTokens.Accent : UiTokens.LineCard;
            _style.BgColor = selected ? UiTokens.SurfaceSelected : UiTokens.Surface1;
            if (!selected && (_stateColor == UiTokens.Danger || _stateColor == UiTokens.DangerText || _stateColor == UiTokens.Warning))
                _style.BorderColor = _stateColor;
        }

        protected override void Construct()
        {
            MouseFilter = MouseFilterEnum.Ignore;
            CustomMinimumSize = new Vector2(ChipWidth, ChipHeight);
            Size = new Vector2(ChipWidth, ChipHeight);

            _style = UiKit.Frame(UiTokens.RadiusSm, UiTokens.Surface1, UiTokens.LineCard);
            _style.BorderWidthLeft = 3;

            _panel = new PanelContainer { MouseFilter = MouseFilterEnum.Stop };
            _panel.AddThemeStyleboxOverride("panel", _style);
            _panel.GuiInput += OnGuiInput;
            AddChild(_panel);
            _panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", UiTokens.Space3);
            margin.AddThemeConstantOverride("margin_right", 18);
            margin.AddThemeConstantOverride("margin_top", 6);
            margin.AddThemeConstantOverride("margin_bottom", 7);
            _panel.AddChild(margin);

            var column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", 3);
            margin.AddChild(column);

            _role = UiKit.Label(string.Empty, UiTokens.FontMicro, UiTokens.Faint);
            _role.Uppercase = true;
            _role.ClipText = true;
            column.AddChild(_role);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            _dot = new ColorRect { Color = UiTokens.Success, CustomMinimumSize = new Vector2(7, 7), MouseFilter = MouseFilterEnum.Ignore };
            _dot.SizeFlagsVertical = SizeFlags.ShrinkBegin;
            row.AddChild(_dot);

            _name = UiKit.Label(string.Empty, UiTokens.FontBodySmall, UiTokens.InkBright);
            _name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _name.MaxLinesVisible = 3;
            row.AddChild(_name);
            column.AddChild(row);

            _meta = UiKit.Label(string.Empty, UiTokens.FontTiny, UiTokens.Faint);
            _meta.SizeFlagsVertical = SizeFlags.ExpandFill;
            _meta.VerticalAlignment = VerticalAlignment.Bottom;
            column.AddChild(_meta);
        }

        private void OnGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton button && button.ButtonIndex == MouseButton.Left)
            {
                if (button.Pressed)
                {
                    _dragging = true;
                    _moved = false;
                    Selected?.Invoke(this);
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
                var layer = GetParent<Control>();
                var card = layer?.GetParent<Control>() as CollectionGroupCard;
                var bounds = layer?.Size ?? Vector2.Zero;
                if (card != null)
                    bounds = new Vector2(Mathf.Max(bounds.X, card.Size.X), Mathf.Max(bounds.Y, card.Size.Y));

                var max = new Vector2(
                    Mathf.Max(0f, bounds.X - Size.X - 2f),
                    Mathf.Max(0f, bounds.Y - Size.Y - 2f));
                var minY = Mathf.Min(card?.ModsTop ?? 0f, max.Y);
                var desired = Position + motion.Relative;
                var clamped = new Vector2(Mathf.Clamp(desired.X, 0f, max.X), Mathf.Clamp(desired.Y, minY, max.Y));

                if (clamped.DistanceTo(Position) > 0.5f)
                    _moved = true;

                Position = clamped;
                Moved?.Invoke(this, clamped);
                AcceptEvent();
            }
        }
    }
}
