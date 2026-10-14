using Cthangover.Core.Characters;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Theming;
using Godot;

using Cthangover.Mods.Interface.Theming;
namespace Cthangover.Core.UI.CharacterPanel
{
    /// <summary>
    /// Visual representation of a single <see cref="ActionCharacter"/> inside
    /// a character slot or in the action pool. The static layout (frame panel,
    /// accent stripe, icon well, name row, badge and property containers)
    /// lives in <c>scenes/ui/action_card.tscn</c>; this behaviour resolves
    /// those nodes and owns only the dynamic layer: type accent colour,
    /// translated name, ID watermark, badges, property chips, hover/selected
    /// frames and the compact (pool) vs expanded (slot) size states.
    /// <para/>
    /// Instances are created through <see cref="ModScenePrefab"/> (the card
    /// scene), never via <c>new</c> — the node tree comes from the scene.
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

        /// <summary>Binds hover feedback; node references resolve lazily so the card can be bound before entering the tree.</summary>
        public ActionCardBehaviour()
        {
            MouseFilter = MouseFilterEnum.Stop;
            MouseEntered += OnMouseEntered;
            MouseExited += OnMouseExited;
        }

        /// <summary>Re-anchors to the parent's full rect for slot placement, mirroring the scene-root layout.</summary>
        public override void _Ready()
        {
        }

        /// <summary>
        /// Binds the action data and refreshes every rendered element: icon,
        /// translated name, ID watermark, type/cost/cooldown badges, property
        /// chips and the rich tooltip.
        /// </summary>
        public void SetAction(ActionCharacter action)
        {
            ActionData = action;
            ResolveNodes();
            if (action == null || _frame == null)
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
            ResolveNodes();
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

        private void ResolveNodes()
        {
            _frame ??= GetNodeOrNull<Panel>("Frame");
            _accent ??= GetNodeOrNull<ColorRect>("Accent");
            _icon ??= GetNodeOrNull<TextureRect>("Icon");
            _nameLabel ??= GetNodeOrNull<Label>("Body/NameRow/NameLabel");
            _idLabel ??= GetNodeOrNull<Label>("Body/NameRow/IdLabel");
            _badgesRow ??= GetNodeOrNull<HBoxContainer>("Body/BadgesRow");
            _propsRow ??= GetNodeOrNull<HFlowContainer>("Body/PropsRow");

            if (_frame != null && !_frame.HasThemeStyleboxOverride("panel"))
                _frame.AddThemeStyleboxOverride("panel", NormalFrame);
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
            ResolveNodes();
            if (_frame == null)
                return;
            _frame.AddThemeStyleboxOverride("panel",
                _selected ? SelectedFrame : (_hovered ? HoverFrame : NormalFrame));
        }
    }
}
