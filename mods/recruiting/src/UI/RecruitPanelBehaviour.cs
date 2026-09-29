using Cthangover.Core.Characters;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.UI;
using Cthangover.Core.UI.CharacterPanel;
using Cthangover.Core.UI.Theming;
using Godot;

namespace Cthangover.Core.Relationship
{
    /// <summary>
    /// Mod-owned panel of captured recruits. It is declared in the mod manifest
    /// as a <c>kind:panel</c> GUI entry, so the core toolbar renders its button
    /// and <see cref="UiHost"/> caches the instance for the whole session; the
    /// core character panel stays unaware of recruiting.
    /// <para/>
    /// The panel is a pure view over <see cref="RecruitingData.Instance"/>: rows
    /// are rebuilt from the roster on every show and after every squad toggle. A
    /// recruit joins the active party through <see cref="RecruitPartyBridge"/>,
    /// which materializes the placeholder card the core character panel and
    /// battle systems consume. Health comes from the recruit's own property bag,
    /// not from the placeholder card, matching the mod's ownership of its data
    /// outside battle.
    /// </summary>
    public class RecruitPanelBehaviour : Widget
    {
        private VBoxContainer _list;
        private Label _statusLabel;

        protected override void OnceConstruct()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;

            var backdrop = new ColorRect();
            backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            backdrop.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(backdrop);
            UiKit.ApplyPanelBackdrop(backdrop);

            var panel = new Panel();
            panel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            panel.OffsetLeft = 40;
            panel.OffsetTop = 70;
            panel.OffsetRight = -40;
            panel.OffsetBottom = -30;
            panel.AddThemeStyleboxOverride("panel", UiKit.Frame(
                UiTokens.RadiusLg, UiTokens.Surface0, UiTokens.Line));
            AddChild(panel);

            var margin = new MarginContainer();
            margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            margin.AddThemeConstantOverride("margin_left", 24);
            margin.AddThemeConstantOverride("margin_right", 24);
            margin.AddThemeConstantOverride("margin_top", 14);
            margin.AddThemeConstantOverride("margin_bottom", 14);
            panel.AddChild(margin);

            var root = new VBoxContainer();
            root.AddThemeConstantOverride("separation", 10);
            margin.AddChild(root);

            root.AddChild(BuildHeader());
            root.AddChild(MakeRule());

            var scroll = new ScrollContainer();
            scroll.SizeFlagsVertical = SizeFlags.ExpandFill;
            scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            root.AddChild(scroll);

            _list = new VBoxContainer();
            _list.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _list.AddThemeConstantOverride("separation", 8);
            scroll.AddChild(_list);

            root.AddChild(MakeRule());

            _statusLabel = ActionCharacterUi.MakeLabel(string.Empty, 12, ActionCharacterUi.Faint);
            root.AddChild(_statusLabel);
        }

        protected override void ShowConstruct()
        {
            Rebuild();
        }

        protected override void HideDestruct()
        {
            ClearList();
        }

        private Control BuildHeader()
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 18);
            row.MouseFilter = MouseFilterEnum.Ignore;

            row.AddChild(ActionCharacterUi.MakeLabel(
                ActionCharacterUi.Translate("ui/recruit/title").ToUpperInvariant(), 22, ActionCharacterUi.InkBright));
            row.AddChild(UiKit.Spacer());

            row.AddChild(ActionCharacterUi.MakeLabel(
                ActionCharacterUi.Translate("ui/recruit/sub"), 11, ActionCharacterUi.Faint));

            var closeButton = new Button
            {
                Text = "✕",
                TooltipText = ActionCharacterUi.Translate("ui/recruit/close"),
                FocusMode = FocusModeEnum.None,
                CustomMinimumSize = new Vector2(38, 38),
            };
            CthangoverTheme.ApplyIcon(closeButton);
            closeButton.Pressed += Hide;
            row.AddChild(closeButton);

            return row;
        }

        private void Rebuild()
        {
            ClearList();

            var roster = RecruitingData.Instance.Data;
            foreach (var recruit in roster)
                _list.AddChild(BuildRow(recruit));

            if (roster.Count == 0)
                _list.AddChild(BuildEmptyLabel());

            _statusLabel.Text = string.Format(
                ActionCharacterUi.Translate("ui/recruit/status"), roster.Count, CountInSquad());
        }

        private Control BuildRow(Recruit recruit)
        {
            var template = CharacterFactory.Instance.Get(recruit.CharacterID);
            var inSquad = RecruitPartyBridge.IsInSquad(recruit);

            var card = new PanelContainer();
            card.AddThemeStyleboxOverride("panel", UiKit.Frame(
                UiTokens.RadiusMd, UiTokens.SurfaceRaised,
                inSquad ? UiTokens.AccentLine : UiTokens.Line));

            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_left", 12);
            margin.AddThemeConstantOverride("margin_right", 12);
            margin.AddThemeConstantOverride("margin_top", 10);
            margin.AddThemeConstantOverride("margin_bottom", 10);
            card.AddChild(margin);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            row.MouseFilter = MouseFilterEnum.Ignore;
            margin.AddChild(row);

            row.AddChild(BuildPortrait(template));

            var info = new VBoxContainer();
            info.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            info.AddThemeConstantOverride("separation", 4);
            info.MouseFilter = MouseFilterEnum.Ignore;
            row.AddChild(info);

            var nameLabel = ActionCharacterUi.MakeLabel(
                ActionCharacterUi.Translate(template?.Name ?? recruit.CharacterID), 18, UiTokens.InkBright);
            nameLabel.ClipText = true;
            info.AddChild(nameLabel);

            var subLabel = ActionCharacterUi.MakeLabel(recruit.CharacterID, 10, UiTokens.Faint);
            subLabel.ClipText = true;
            info.AddChild(subLabel);

            var health = recruit.Properties.GetInt(Recruit.PROP_HEALTH);
            var maxHealth = template?.Attributes?.Health?.BaseValue ?? 0;
            var healthText = maxHealth > 0 ? $"{health}/{maxHealth}" : health.ToString();
            info.AddChild(UiKit.StatChip(
                ActionCharacterUi.Translate("ui/recruit/hp"), healthText, UiTokens.HpText));

            var button = UiKit.Button(
                ActionCharacterUi.Translate(inSquad ? "ui/recruit/remove" : "ui/recruit/add"),
                inSquad ? ButtonKind.Danger : ButtonKind.Primary);
            button.FocusMode = FocusModeEnum.None;
            button.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            var captured = recruit;
            button.Pressed += () => OnToggleSquad(captured);
            row.AddChild(button);

            return card;
        }

        private static Control BuildPortrait(Character template)
        {
            var portraitPanel = new PanelContainer();
            portraitPanel.CustomMinimumSize = new Vector2(58, 58);
            portraitPanel.AddThemeStyleboxOverride("panel", UiKit.Frame(
                UiTokens.RadiusMd, UiTokens.Surface2, UiTokens.Line));
            portraitPanel.MouseFilter = MouseFilterEnum.Ignore;

            var portrait = new TextureRect
            {
                Texture = template?.Image,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            portraitPanel.AddChild(portrait);
            return portraitPanel;
        }

        private Control BuildEmptyLabel()
        {
            var empty = UiKit.EmptyState(ActionCharacterUi.Translate("ui/recruit/empty"));
            empty.CustomMinimumSize = new Vector2(0, 120);
            return empty;
        }

        private void OnToggleSquad(Recruit recruit)
        {
            if (RecruitPartyBridge.IsInSquad(recruit))
                RecruitPartyBridge.RemoveFromSquad(recruit);
            else
                RecruitPartyBridge.AddToSquad(recruit);

            Rebuild();
        }

        private int CountInSquad()
        {
            var count = 0;
            foreach (var recruit in RecruitingData.Instance.Data)
            {
                if (RecruitPartyBridge.IsInSquad(recruit))
                    count++;
            }
            return count;
        }

        private void ClearList()
        {
            if (_list == null)
                return;
            foreach (var child in _list.GetChildren())
                child.QueueFree();
        }

        private static Control MakeRule()
        {
            var rule = new Panel();
            rule.CustomMinimumSize = new Vector2(0, 1);
            rule.AddThemeStyleboxOverride("panel", UiKit.Frame(0, UiTokens.Line, UiTokens.Line));
            rule.MouseFilter = MouseFilterEnum.Ignore;
            return rule;
        }
    }
}
