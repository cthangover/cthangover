using Cthangover.Core.Characters;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.Core.UI.CharacterPanel
{
    /// <summary>
    /// Visual representation of a single <see cref="ActionCharacter"/> inside
    /// a character slot or in the action pool. Built entirely in code (like
    /// the inventory cells) because the card's visual language — type accent
    /// stripe, badges for type/cost/cooldown, a mod-driven property chip row
    /// and hover/selected frames — cannot be expressed through tscn theme
    /// overrides alone.
    ///
    /// The card never handles input itself: <see cref="CharacterPanelBehaviour"/>
    /// owns drag orchestration through its node-level <c>_Input</c> (GuiInput
    /// stops firing once the cursor leaves the card mid-drag). The card only
    /// exposes state setters (<see cref="SetSelected"/>, <see cref="SetCompact"/>)
    /// and is moved between containers by the panel/slot.
    /// </summary>
    public partial class ActionCardBehaviour : Control
    {
        private const float NormalHeight = 86f;
        private const float CompactHeight = 60f;

        /// <summary>Factory action bound to this card; <c>null</c> before <see cref="SetAction"/>.</summary>
        public ActionCharacter ActionData { get; private set; }

        /// <summary>Slot that currently owns the card, or <c>null</c> when the card lives in the pool.</summary>
        public ActionSlotBehaviour CurrentSlot { get; set; }

        private static readonly StyleBoxFlat NormalFrame = UiKit.Frame(
            UiTokens.RadiusMd, UiTokens.Surface1, UiTokens.LineCard);
        private static readonly StyleBoxFlat HoverFrame = UiKit.Frame(
            UiTokens.RadiusMd, UiTokens.SurfaceHover, UiTokens.LineStrong);
        private static readonly StyleBoxFlat SelectedFrame = UiKit.Frame(
            UiTokens.RadiusMd, UiTokens.SurfaceSelected, UiTokens.Accent);

        private Panel _frame;
        private ColorRect _accent;
        private TextureRect _icon;
        private Label _nameLabel;
        private Label _idLabel;
        private HBoxContainer _badgesRow;
        private HFlowContainer _propsRow;
        private bool _compact;
        private bool _selected;
        private bool _hovered;
        private bool _built;

        public ActionCardBehaviour()
        {
            MouseFilter = MouseFilterEnum.Stop;
            CustomMinimumSize = new Vector2(0, NormalHeight);
            MouseEntered += OnMouseEntered;
            MouseExited += OnMouseExited;
            EnsureBuilt();
        }

        /// <summary>
        /// Binds the action data and refreshes every rendered element: icon,
        /// translated name, ID watermark, type/cost/cooldown badges, property
        /// chips and the rich tooltip.
        /// </summary>
        public void SetAction(ActionCharacter action)
        {
            ActionData = action;
            if (action == null)
                return;

            _icon.Texture = action.Image;
            _nameLabel.Text = ActionCharacterUi.Translate(action.Name);
            _idLabel.Text = action.ID;
            TooltipText = ActionCharacterUi.BuildTooltip(action);

            BuildBadges(action);
            BuildProperties(action);
        }

        /// <summary>
        /// Compact mode drops the property chip row and shrinks the card to the
        /// single-line pool layout; slot cards use the expanded layout.
        /// </summary>
        public void SetCompact(bool compact)
        {
            _compact = compact;
            CustomMinimumSize = new Vector2(0, compact ? CompactHeight : NormalHeight);
            if (_propsRow != null)
                _propsRow.Visible = !compact && _propsRow.GetChildCount() > 0;
            if (_icon != null)
            {
                var half = compact ? 22 : 26;
                _icon.OffsetTop = -half;
                _icon.OffsetBottom = half;
                _icon.OffsetRight = compact ? 52 : 60;
            }
        }

        /// <summary>Gold frame for click-selected cards; drag state is handled by the panel.</summary>
        public void SetSelected(bool selected)
        {
            _selected = selected;
            ApplyFrame();
        }

        /// <summary>
        /// Returns the card to its owning slot, or leaves pool cards to the
        /// panel — a pool card has no slot to return to, so the caller must
        /// restore it through <see cref="ActionPoolBehaviour"/>.
        /// </summary>
        public void ReturnToOrigin()
        {
            if (CurrentSlot == null)
                return;

            if (GetParent() != CurrentSlot)
            {
                GetParent()?.RemoveChild(this);
                CurrentSlot.AddChild(this);
            }
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        }

        /// <summary>Marks the card as detached from every container while it follows the cursor.</summary>
        public void SetDragging(bool dragging)
        {
            Modulate = dragging ? UiTokens.Alpha(UiTokens.White, 0.55f) : UiTokens.White;
        }

        private void EnsureBuilt()
        {
            if (_built)
                return;
            _built = true;

            _frame = new Panel();
            _frame.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _frame.AddThemeStyleboxOverride("panel", NormalFrame);
            _frame.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_frame);

            _accent = new ColorRect();
            _accent.SetAnchorsAndOffsetsPreset(LayoutPreset.LeftWide);
            _accent.OffsetRight = 4;
            _accent.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_accent);

            _icon = new TextureRect();
            _icon.SetAnchorsAndOffsetsPreset(LayoutPreset.CenterLeft);
            _icon.OffsetLeft = 8;
            _icon.OffsetRight = 60;
            _icon.OffsetTop = -26;
            _icon.OffsetBottom = 26;
            _icon.CustomMinimumSize = new Vector2(52, 52);
            _icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            _icon.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_icon);

            var body = new VBoxContainer();
            body.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            body.OffsetLeft = 68;
            body.OffsetTop = 6;
            body.OffsetRight = -8;
            body.OffsetBottom = -6;
            body.AddThemeConstantOverride("separation", 4);
            body.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(body);

            var nameRow = new HBoxContainer();
            nameRow.AddThemeConstantOverride("separation", 8);
            nameRow.MouseFilter = MouseFilterEnum.Ignore;
            body.AddChild(nameRow);

            _nameLabel = ActionCharacterUi.MakeLabel(string.Empty, 14, UiTokens.InkBright);
            _nameLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _nameLabel.ClipText = true;
            nameRow.AddChild(_nameLabel);

            _idLabel = ActionCharacterUi.MakeLabel(string.Empty, 9, UiTokens.Faint);
            _idLabel.VerticalAlignment = VerticalAlignment.Center;
            nameRow.AddChild(_idLabel);

            _badgesRow = new HBoxContainer();
            _badgesRow.AddThemeConstantOverride("separation", 5);
            _badgesRow.MouseFilter = MouseFilterEnum.Ignore;
            body.AddChild(_badgesRow);

            _propsRow = new HFlowContainer();
            _propsRow.AddThemeConstantOverride("h_separation", 5);
            _propsRow.AddThemeConstantOverride("v_separation", 3);
            _propsRow.MouseFilter = MouseFilterEnum.Ignore;
            body.AddChild(_propsRow);
        }

        private void BuildBadges(ActionCharacter action)
        {
            ClearChildren(_badgesRow);

            var accent = ActionCharacterUi.AccentColor(action.Type);
            _accent.Color = accent;
            _badgesRow.AddChild(ActionCharacterUi.MakeBadge(
                ActionCharacterUi.Translate(ActionCharacterUi.TypeLabelKey(action.Type)).ToUpperInvariant(), accent));

            var costText = ActionCharacterUi.CostText(action);
            if (costText != null)
                _badgesRow.AddChild(ActionCharacterUi.MakeBadge(costText, UiTokens.AccentBright));

            var cooldownText = ActionCharacterUi.CooldownText(action);
            if (cooldownText != null)
                _badgesRow.AddChild(ActionCharacterUi.MakeBadge(cooldownText, UiTokens.Cooldown));
        }

        private void BuildProperties(ActionCharacter action)
        {
            ClearChildren(_propsRow);
            foreach (var pair in ActionCharacterUi.SortedProperties(action))
                _propsRow.AddChild(ActionCharacterUi.MakePropertyChip(pair.Key, pair.Value));

            _propsRow.Visible = !_compact && _propsRow.GetChildCount() > 0;
        }

        private static void ClearChildren(Node parent)
        {
            if (parent == null)
                return;
            foreach (var child in parent.GetChildren())
                child.QueueFree();
        }

        private void OnMouseEntered()
        {
            _hovered = true;
            ApplyFrame();
        }

        private void OnMouseExited()
        {
            _hovered = false;
            ApplyFrame();
        }

        private void ApplyFrame()
        {
            if (_frame == null)
                return;
            _frame.AddThemeStyleboxOverride("panel",
                _selected ? SelectedFrame : (_hovered ? HoverFrame : NormalFrame));
        }
    }
}
