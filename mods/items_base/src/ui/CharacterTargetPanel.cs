using System;
using Cthangover.Core.Characters;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.Core.UI.Inventory
{
    /// <summary>
    /// Inline target picker for <see cref="ItemType.TargetUsed"/> items,
    /// embedded directly into the item passport instead of replacing the
    /// whole right column. Each party member is a button row with a health
    /// bar, current/max HP and the translated name; a cancel button closes
    /// the picker. Extends <see cref="VBoxContainer"/> because the previous
    /// Control-based version left its children unmanaged — a plain Control
    /// never laid them out, so title, rows and cancel button collapsed into
    /// the top-left corner.
    /// </summary>
    public partial class CharacterTargetPanel : VBoxContainer
    {
        /// <summary>Raised when the player picks a target; the effect is applied by the inventory panel.</summary>
        public event Action<CharacterInfoData> CharacterSelected;
        /// <summary>Raised when the player aborts target selection.</summary>
        public event Action Cancelled;

        /// <summary>Builds the picker inline: title, one row per party member, and a cancel button.</summary>
        public CharacterTargetPanel()
        {
            SizeFlagsVertical = SizeFlags.ExpandFill;
            MouseFilter = MouseFilterEnum.Stop;
            AddThemeConstantOverride("separation", 7);

            var title = new Label();
            title.Text = InventoryItemUi.Translate("ui/inventory/select_target").ToUpperInvariant();
            title.AddThemeFontSizeOverride("font_size", 10);
            title.AddThemeColorOverride("font_color", UiTokens.TypeTarget);
            title.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(title);

            var characterData = GameData.Instance.Runtime.CharacterData;
            foreach (var id in characterData.BattleSet)
            {
                if (!characterData.Characters.TryGetValue(id, out var info))
                    continue;
                AddChild(BuildTargetRow(info));
            }

            var cancel = new Button();
            cancel.Text = InventoryItemUi.Translate("ui/inventory/cancel");
            cancel.FocusMode = FocusModeEnum.None;
            cancel.AddThemeFontSizeOverride("font_size", 13);
            cancel.AddThemeStyleboxOverride("normal", MakeCancelStyle(false));
            cancel.AddThemeStyleboxOverride("hover", MakeCancelStyle(true));
            cancel.AddThemeStyleboxOverride("pressed", MakeCancelStyle(true));
            cancel.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            cancel.AddThemeColorOverride("font_color", UiTokens.Body);
            cancel.AddThemeColorOverride("font_hover_color", Colors.White);
            cancel.Pressed += OnCancelPressed;
            AddChild(cancel);
        }

        private Button BuildTargetRow(CharacterInfoData info)
        {
            var template = CharacterTemplateRegistry.Get(info.ID);
            var displayName = InventoryItemUi.Translate(template?.Name ?? info.ID);
            var health = info.Attributes?.Health;
            var value = health?.Value ?? 0;
            var max = health?.BaseValue ?? 0;

            var button = new Button();
            button.FocusMode = FocusModeEnum.None;
            button.CustomMinimumSize = new Vector2(0, 46);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            button.AddThemeStyleboxOverride("normal", MakeRowStyle(false));
            button.AddThemeStyleboxOverride("hover", MakeRowStyle(true));
            button.AddThemeStyleboxOverride("pressed", MakeRowStyle(true));
            button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

            var row = new HBoxContainer();
            row.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            row.OffsetLeft = 12;
            row.OffsetRight = -12;
            row.AddThemeConstantOverride("separation", 10);
            row.MouseFilter = MouseFilterEnum.Ignore;
            button.AddChild(row);

            var name = new Label();
            name.Text = displayName;
            name.AddThemeFontSizeOverride("font_size", 14);
            name.AddThemeColorOverride("font_color", UiTokens.Ink);
            name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            name.ClipText = true;
            name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            name.VerticalAlignment = VerticalAlignment.Center;
            name.MouseFilter = MouseFilterEnum.Ignore;
            row.AddChild(name);

            row.AddChild(BuildHealthBar(value, max));

            var hp = new Label();
            hp.Text = value + "/" + max;
            hp.AddThemeFontSizeOverride("font_size", 12);
            hp.AddThemeColorOverride("font_color", UiTokens.Muted);
            hp.VerticalAlignment = VerticalAlignment.Center;
            hp.MouseFilter = MouseFilterEnum.Ignore;
            row.AddChild(hp);

            var captured = info;
            button.Pressed += () => CharacterSelected?.Invoke(captured);
            return button;
        }

        private void OnCancelPressed()
        {
            Cancelled?.Invoke();
        }

        private static ProgressBar BuildHealthBar(int value, int max)
        {
            var ratio = max > 0 ? (float)value / max : 1f;

            var bar = new ProgressBar();
            bar.CustomMinimumSize = new Vector2(96, 7);
            bar.MaxValue = max > 0 ? max : 1;
            bar.Value = value;
            bar.ShowPercentage = false;
            bar.MouseFilter = MouseFilterEnum.Ignore;
            bar.AddThemeStyleboxOverride("background", MakeBarStyle(UiTokens.LineDisabled));
            bar.AddThemeStyleboxOverride("fill", MakeBarStyle(ratio < 0.4f
                ? UiTokens.HpLow
                : UiTokens.HpOk));
            return bar;
        }

        private static StyleBoxFlat MakeRowStyle(bool hover)
        {
            var style = new StyleBoxFlat();
            style.BgColor = hover ? UiTokens.TargetHover : UiTokens.Surface2;
            style.BorderColor = hover ? UiTokens.TargetLine : UiTokens.Line;
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

        private static StyleBoxFlat MakeBarStyle(Color color)
        {
            var style = new StyleBoxFlat();
            style.BgColor = color;
            style.CornerRadiusTopLeft = 4;
            style.CornerRadiusTopRight = 4;
            style.CornerRadiusBottomLeft = 4;
            style.CornerRadiusBottomRight = 4;
            return style;
        }

        private static StyleBoxFlat MakeCancelStyle(bool hover)
        {
            var style = new StyleBoxFlat();
            style.BgColor = hover ? UiTokens.BtnHover : UiTokens.Surface3;
            style.BorderColor = hover ? UiTokens.Alpha(UiTokens.Accent, 0.55f) : UiTokens.Line;
            style.BorderWidthLeft = 1;
            style.BorderWidthRight = 1;
            style.BorderWidthTop = 1;
            style.BorderWidthBottom = 1;
            style.CornerRadiusTopLeft = 4;
            style.CornerRadiusTopRight = 4;
            style.CornerRadiusBottomLeft = 4;
            style.CornerRadiusBottomRight = 4;
            style.ContentMarginLeft = 12;
            style.ContentMarginRight = 12;
            style.ContentMarginTop = 9;
            style.ContentMarginBottom = 9;
            return style;
        }
    }
}
