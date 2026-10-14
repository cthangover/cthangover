using System;
using Cthangover.Core.Items;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.Core.UI.Inventory
{
    /// <summary>
    /// Interactive cell of the inventory grid. Unlike the old thin cell that
    /// only drew an icon and a stack counter, this one owns the whole visual
    /// language of a slot: the frame (normal/hover/selected), the type accent
    /// dot, the short action tag and the dimmed state for inert items. It is
    /// built entirely in code because the grid needs pixel-accurate
    /// <see cref="StyleBoxFlat"/> frame swapping that theme overrides in the
    /// tscn pipeline cannot express.
    ///
    /// Click handling lives inside the cell: it raises <see cref="Clicked"/>
    /// after consuming the mouse event, instead of the panel subscribing to
    /// <c>GuiInput</c> from outside, so event propagation is stopped by the
    /// control that actually owns the click.
    /// </summary>
    public partial class InventoryItemBehaviour : ListItem<IItemContainer>
    {
        /// <summary>Raised on a left-click after the cell consumed the event. The payload is the cell itself.</summary>
        public event Action<InventoryItemBehaviour> Clicked;

        private static readonly StyleBoxFlat NormalFrame = MakeFrame(UiTokens.LineSoft, UiTokens.Surface1);
        private static readonly StyleBoxFlat HoverFrame = MakeFrame(UiTokens.LineStrong, UiTokens.SurfaceHover);
        private static readonly StyleBoxFlat SelectedFrame = MakeFrame(UiTokens.Accent, UiTokens.SurfaceSelected);
        private static readonly StyleBoxFlat TagStyle = MakeTagStyle();
        private static readonly Color IconDimmed = UiTokens.Alpha(UiTokens.White, 0.38f);

        private Panel _frame;
        private TextureRect _icon;
        private Label _count;
        private Panel _dot;
        private StyleBoxFlat _dotStyle;
        private PanelContainer _tag;
        private Label _tagLabel;
        private bool _built;
        private bool _selected;
        private bool _hovered;

        /// <summary>Stops mouse propagation and wires hover/click handling before any model is bound.</summary>
        public InventoryItemBehaviour()
        {
            MouseFilter = MouseFilterEnum.Stop;
            GuiInput += OnGuiInput;
            MouseEntered += OnMouseEntered;
            MouseExited += OnMouseExited;
        }

        /// <summary>
        /// Binds the item stack and lazily builds the visual tree on first
        /// use; repeated binds only refresh the rendered data.
        /// </summary>
        public override void Construct(IItemContainer model)
        {
            base.Construct(model);
            EnsureBuilt();
            UpdateInfo();
        }

        /// <summary>
        /// Pushes the bound stack into the visuals: icon, stack counter
        /// (hidden for singletons), type accent dot, action tag and the
        /// dimmed state for items without a usable action.
        /// </summary>
        public void UpdateInfo()
        {
            if (!_built || Model?.Item == null)
                return;

            var item = Model.Item;
            _icon.Texture = item.Sprite;
            _count.Text = Model.Count > 1 ? "×" + Model.Count : string.Empty;
            _count.Visible = Model.Count > 1;
            _dotStyle.BgColor = InventoryItemUi.AccentColor(item.ItemType);

            var tagKey = InventoryItemUi.TagKey(item);
            _tag.Visible = tagKey != null;
            if (tagKey != null)
                _tagLabel.Text = InventoryItemUi.Translate(tagKey);

            var dimmed = !InventoryItemUi.IsUsable(item);
            _icon.Modulate = dimmed ? IconDimmed : Colors.White;
            _count.Modulate = dimmed ? IconDimmed : Colors.White;
        }

        /// <summary>Switches the frame to the gold selected state; the panel owns this flag.</summary>
        public void SetSelected(bool selected)
        {
            _selected = selected;
            ApplyFrame();
        }

        /// <summary>Removes the cell from the scene tree; the grid rebuilds cells instead of recycling them.</summary>
        public override void Destruct()
        {
            QueueFree();
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

            _dot = new Panel();
            _dot.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
            _dot.OffsetLeft = 6;
            _dot.OffsetTop = 6;
            _dot.OffsetRight = 13;
            _dot.OffsetBottom = 13;
            _dot.MouseFilter = MouseFilterEnum.Ignore;
            _dotStyle = new StyleBoxFlat();
            _dotStyle.CornerRadiusTopLeft = 2;
            _dotStyle.CornerRadiusTopRight = 2;
            _dotStyle.CornerRadiusBottomLeft = 2;
            _dotStyle.CornerRadiusBottomRight = 2;
            _dot.AddThemeStyleboxOverride("panel", _dotStyle);
            AddChild(_dot);

            _icon = new TextureRect();
            _icon.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _icon.OffsetLeft = 12;
            _icon.OffsetTop = 12;
            _icon.OffsetRight = -12;
            _icon.OffsetBottom = -12;
            _icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            _icon.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_icon);

            _count = new Label();
            _count.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
            _count.OffsetLeft = -70;
            _count.OffsetTop = -26;
            _count.OffsetRight = -6;
            _count.OffsetBottom = -3;
            _count.HorizontalAlignment = HorizontalAlignment.Right;
            _count.VerticalAlignment = VerticalAlignment.Center;
            _count.AddThemeFontSizeOverride("font_size", 13);
            _count.AddThemeColorOverride("font_color", UiTokens.Stack);
            _count.AddThemeColorOverride("font_outline_color", new Color(0f, 0f, 0f, 0.9f));
            _count.AddThemeConstantOverride("outline_size", 4);
            _count.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(_count);

            var tagRow = new HBoxContainer();
            tagRow.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
            tagRow.OffsetLeft = 5;
            tagRow.OffsetTop = 4;
            tagRow.OffsetRight = -5;
            tagRow.OffsetBottom = 24;
            tagRow.Alignment = BoxContainer.AlignmentMode.End;
            tagRow.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(tagRow);

            _tag = new PanelContainer();
            _tag.AddThemeStyleboxOverride("panel", TagStyle);
            _tag.MouseFilter = MouseFilterEnum.Ignore;
            tagRow.AddChild(_tag);

            _tagLabel = new Label();
            _tagLabel.AddThemeFontSizeOverride("font_size", 9);
            _tagLabel.AddThemeColorOverride("font_color", UiTokens.AccentBright);
            _tagLabel.MouseFilter = MouseFilterEnum.Ignore;
            _tag.AddChild(_tagLabel);
        }

        private void OnGuiInput(InputEvent @event)
        {
            if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
            {
                Clicked?.Invoke(this);
                AcceptEvent();
            }
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
            if (!_built || _frame == null)
                return;
            _frame.AddThemeStyleboxOverride("panel",
                _selected ? SelectedFrame : (_hovered ? HoverFrame : NormalFrame));
        }

        private static StyleBoxFlat MakeFrame(Color border, Color background)
        {
            var style = new StyleBoxFlat();
            style.BgColor = background;
            style.BorderColor = border;
            style.BorderWidthLeft = 1;
            style.BorderWidthRight = 1;
            style.BorderWidthTop = 1;
            style.BorderWidthBottom = 1;
            style.CornerRadiusTopLeft = 6;
            style.CornerRadiusTopRight = 6;
            style.CornerRadiusBottomLeft = 6;
            style.CornerRadiusBottomRight = 6;
            return style;
        }

        private static StyleBoxFlat MakeTagStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = UiTokens.SlotTagBg;
            style.BorderColor = UiTokens.AccentLine;
            style.BorderWidthLeft = 1;
            style.BorderWidthRight = 1;
            style.BorderWidthTop = 1;
            style.BorderWidthBottom = 1;
            style.CornerRadiusTopLeft = 3;
            style.CornerRadiusTopRight = 3;
            style.CornerRadiusBottomLeft = 3;
            style.CornerRadiusBottomRight = 3;
            style.ContentMarginLeft = 5;
            style.ContentMarginRight = 5;
            style.ContentMarginTop = 1;
            style.ContentMarginBottom = 1;
            return style;
        }
    }
}
