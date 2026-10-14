using Cthangover.Core.Characters;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.UI;
using Cthangover.Core.UI.CharacterPanel;
using Cthangover.Core.UI.Theming;
using Godot;

using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Mods.Interface.Theming;
namespace Cthangover.Core.Relationship
{
    /// <summary>
    /// Mod-owned panel of captured recruits. It is declared in the mod manifest
    /// as a `kind:panel` GUI entry, so the core toolbar renders its button
    /// and the host caches the instance for the whole session; the core
    /// character panel stays unaware of recruiting.
    /// <para/>
    /// The static layout (backdrop, framed panel, header row, scroll host,
    /// rules and status footer) lives in <c>scenes/recruit_panel.tscn</c>;
    /// this behaviour resolves those nodes and owns the dynamic layer: roster
    /// rows rebuilt from <see cref="RecruitingData.Instance"/> on every show
    /// and after every squad toggle. A recruit joins the active party through
    /// <see cref="RecruitPartyBridge"/>, which materializes the placeholder
    /// card the core character panel and battle systems consume.
    /// </summary>
    public class RecruitPanelBehaviour : Widget
    {
        // Scene nodes (recruit_panel.tscn)
        private Label _title;
        private Label _sub;
        private Button _closeButton;
        private VBoxContainer _list;
        private Label _statusLabel;

        /// <summary>Binds scene nodes and applies theme and localized captions.</summary>
        protected override void OnceConstruct()
        {
            MouseFilter = MouseFilterEnum.Stop;

            _title = GetNode<Label>("Panel/Margin/Root/Header/Title");
            _sub = GetNode<Label>("Panel/Margin/Root/Header/Sub");
            _closeButton = GetNode<Button>("Panel/Margin/Root/Header/CloseButton");
            _list = GetNode<VBoxContainer>("Panel/Margin/Root/Scroll/List");
            _statusLabel = GetNode<Label>("Panel/Margin/Root/StatusLabel");

            ApplyTheme();
            ApplyCaptions();

            _closeButton.Pressed += Hide;
        }

        /// <summary>Theme overrides for scene nodes that depend on UiKit tokens.</summary>
        private void ApplyTheme()
        {
            GetNode<Panel>("Panel").AddThemeStyleboxOverride("panel", UiKit.Frame(
                UiTokens.RadiusLg, UiTokens.Surface0, UiTokens.Line));

            _title.AddThemeColorOverride("font_color", ActionCharacterUi.InkBright);
            _sub.AddThemeColorOverride("font_color", UiTokens.Faint);
            _statusLabel.AddThemeColorOverride("font_color", UiTokens.Faint);

            var backdrop = GetNode<ColorRect>("Backdrop");
            UiKit.ApplyPanelBackdrop(backdrop);

            foreach (var ruleName in new[] { "Rule1", "Rule2" })
            {
                var rule = GetNode<Panel>($"Panel/Margin/Root/{ruleName}");
                rule.AddThemeStyleboxOverride("panel", UiKit.Frame(0, UiTokens.Line, UiTokens.Line));
            }

            CthangoverTheme.ApplyIcon(_closeButton);
        }

        /// <summary>Localized captions for all static labels of the scene.</summary>
        private void ApplyCaptions()
        {
            _title.Text = ActionCharacterUi.Translate("ui/recruit/title").ToUpperInvariant();
            _sub.Text = ActionCharacterUi.Translate("ui/recruit/sub");
            _closeButton.TooltipText = ActionCharacterUi.Translate("ui/recruit/close");
        }

        /// <summary>Rebuilds the roster rows from current data each time the panel is opened.</summary>
        protected override void ShowConstruct()
        {
            Rebuild();
        }

        /// <summary>Frees the row nodes when the panel is hidden so the list does not go stale.</summary>
        protected override void HideDestruct()
        {
            ClearList();
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
    }
}
