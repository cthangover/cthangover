namespace Cthangover.Mods.Stats
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Cthangover.Core.Events;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Interface.Base.Lists;
    using Cthangover.Mods.Interface.Theming;
    using Godot;

    /// <summary>
    /// The character sheet (toolbar panel "sheet"): the party on the left, the selected
    /// member's portrait, archetype, level and exp, every visible stat grouped by its
    /// <see cref="StatInfo.Group"/> with the part that traits add shown next to the value,
    /// and below them the blocks other mods contribute (<see cref="ISheetSection"/>).
    /// <para/>
    /// The sheet only reads <see cref="StatsService"/>, so it shows whatever stats the
    /// game's data defines — a novel's courage and trust as well as an RPG's attributes —
    /// and redraws on <see cref="StatsChangedEvent"/> while it is open. A game without a
    /// party shows the player's own sheet.
    /// </summary>
    public partial class StatSheetPanel : Widget
    {
        private ColorRect backdrop;
        private Panel window;
        private Panel rule;
        private Label title;
        private Button closeButton;
        private Label partyLabel;
        private VBoxContainer partyList;
        private TextureRect portrait;
        private Label nameLabel;
        private Label archetypeLabel;
        private Label levelLabel;
        private ProgressBar expBar;
        private Label archetypeHint;
        private VBoxContainer statsBox;
        private VBoxContainer sectionsBox;
        private Label emptyLabel;
        private string selected;
        private bool subscribed;
        private bool renderQueued;

        /// <summary>The character shown (a roster id or the player id).</summary>
        public string SelectedId => selected;

        /// <inheritdoc />
        public override void _Ready()
        {
            base._Ready();
            ResolveNodes();
            ApplyStyles();
            if (closeButton != null)
                closeButton.Pressed += Hide;
            if (Visible)
            {
                EnsureConstructed();
                ShowConstruct();
            }
        }

        /// <summary>Shows <paramref name="characterId"/> (tests and other panels open the sheet on someone).</summary>
        public void Select(string characterId)
        {
            selected = characterId;
            Render();
        }

        /// <inheritdoc />
        protected override void ShowConstruct()
        {
            if (!subscribed)
            {
                ModEventBus.Subscribe<StatsChangedEvent>(OnStatsChanged);
                subscribed = true;
            }
            var party = Party();
            if (selected == null || !party.Contains(selected))
                selected = party.FirstOrDefault();
            Render();
        }

        /// <inheritdoc />
        protected override void HideDestruct() => Unsubscribe();

        /// <inheritdoc />
        protected override void OnExitTree() => Unsubscribe();

        /// <summary>The characters the sheet lists: battle set first, then the rest of the roster; the player alone without a roster.</summary>
        public static List<string> Party()
        {
            var roster = StatsService.Roster;
            var ids = roster?.Characters.Keys.OrderBy(id => roster.BattleSet.Contains(id) ? 0 : 1).ToList() ?? new List<string>();
            if (ids.Count == 0)
                ids.Add(StatsService.Resolve(null));
            return ids;
        }

        /// <summary>Redraws everything for the selected character.</summary>
        public void Render()
        {
            renderQueued = false;
            if (statsBox == null)
                return;
            RenderParty();
            if (selected == null)
                return;
            RenderHero(selected);
            RenderStats(selected);
            RenderSections(selected);
        }

        private void OnStatsChanged(StatsChangedEvent evt)
        {
            // A section's button may have caused the change: redraw after its signal returns.
            if (!Visible || renderQueued)
                return;
            renderQueued = true;
            Callable.From(Render).CallDeferred();
        }

        private void Unsubscribe()
        {
            if (!subscribed)
                return;
            ModEventBus.Unsubscribe<StatsChangedEvent>(OnStatsChanged);
            subscribed = false;
        }

        private void RenderParty()
        {
            Clear(partyList);
            foreach (var id in Party())
            {
                var button = UiKit.Button($"{NameOf(id)}  ·  {string.Format(Tr("stats/sheet/level_short"), StatsService.LevelOf(id))}");
                button.ToggleMode = true;
                button.ButtonPressed = id == selected;
                button.Alignment = HorizontalAlignment.Left;
                button.Name = "Member_" + id.Replace('/', '_');
                var memberId = id;
                button.Pressed += () => Select(memberId);
                partyList.AddChild(button);
            }
        }

        private void RenderHero(string id)
        {
            var template = StatsService.Template(id);
            portrait.Texture = string.IsNullOrEmpty(template?.Image) ? null : TextureUtils.LoadFromModGroup("characters", template.Image);
            nameLabel.Text = NameOf(id);

            var archetype = StatsService.Archetype(StatsService.ArchetypeOf(id));
            archetypeLabel.Text = archetype == null ? Tr("stats/sheet/no_archetype") : Tr(archetype.Name);
            archetypeHint.Text = archetype == null || string.IsNullOrEmpty(archetype.Description) ? string.Empty : Tr(archetype.Description);
            archetypeHint.Visible = archetypeHint.Text.Length > 0;

            var level = StatsService.LevelOf(id);
            var exp = StatsService.ExpOf(id);
            if (StatProgression.Levels(id) && level < StatProgression.MaxLevel(id))
            {
                var from = StatProgression.ExpFor(id, level);
                var to = StatProgression.ExpFor(id, level + 1);
                levelLabel.Text = string.Format(Tr("stats/sheet/level_exp"), level, exp, to);
                expBar.MinValue = from;
                expBar.MaxValue = Math.Max(from + 1, to);
                expBar.Value = Math.Clamp(exp, from, to);
                expBar.Visible = true;
            }
            else
            {
                levelLabel.Text = string.Format(Tr("stats/sheet/level"), level);
                expBar.Visible = false;
            }
        }

        private void RenderStats(string id)
        {
            Clear(statsBox);
            var sheet = StatsService.Sheet(id);
            var shown = StatsService.Definitions.Where(d => !d.Hidden && sheet.Has(d.ID)).ToList();
            emptyLabel.Visible = shown.Count == 0;
            emptyLabel.Text = Tr("stats/sheet/empty");
            foreach (var group in shown.GroupBy(d => d.Group ?? string.Empty))
            {
                if (group.Key.Length > 0)
                {
                    var key = "stats/group/" + group.Key;
                    var caption = Tr(key);
                    statsBox.AddChild(UiKit.SectionTitle(caption == key ? group.Key : caption, UiTokens.FontBody));
                }
                var grid = new GridContainer { Columns = 2, MouseFilter = MouseFilterEnum.Ignore };
                grid.AddThemeConstantOverride("h_separation", UiTokens.Space6);
                grid.AddThemeConstantOverride("v_separation", UiTokens.Space2);
                foreach (var def in group)
                    grid.AddChild(StatRow(id, def, sheet));
                statsBox.AddChild(grid);
            }
        }

        private Control StatRow(string id, StatInfo def, StatSheet sheet)
        {
            var row = UiKit.Row(UiTokens.Space3);
            row.Name = "Stat_" + def.ID;
            row.TooltipText = string.IsNullOrEmpty(def.Description) ? string.Empty : Tr(def.Description);
            row.MouseFilter = MouseFilterEnum.Pass;
            var name = UiKit.Label(Tr(def.Name), UiTokens.FontBody, UiTokens.Body);
            name.CustomMinimumSize = new Vector2(170, 0);
            row.AddChild(name);
            row.AddChild(UiKit.Label(ValueText(id, def, sheet), UiTokens.FontBodyLarge, UiTokens.InkBright));
            var delta = sheet.Get(def.ID) - sheet.GetBase(def.ID);
            if (Math.Abs(delta) >= 0.5)
            {
                var sign = delta > 0 ? "+" : "−";
                var text = sign + Math.Abs(Math.Round(delta, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
                row.AddChild(UiKit.Label(text, UiTokens.FontSmall, delta > 0 ? UiTokens.SuccessText : UiTokens.DangerText));
            }
            return row;
        }

        private static string ValueText(string id, StatInfo def, StatSheet sheet)
        {
            var max = sheet.Rounded(def.ID);
            if (def.Pool)
                return $"{(int)Math.Round(StatPools.Current(id, def.ID))} / {max}";
            var health = StatsService.RosterEntry(id)?.Attributes?.Health;
            if (health != null && string.Equals(def.Binds, "health", StringComparison.OrdinalIgnoreCase))
                return $"{health.Value} / {health.BaseValue}";
            return max.ToString(CultureInfo.InvariantCulture);
        }

        private void RenderSections(string id)
        {
            Clear(sectionsBox);
            foreach (var section in SheetSections.All)
            {
                try
                {
                    if (!section.AppliesTo(id))
                        continue;
                    var block = UiKit.Column(UiTokens.Space2);
                    block.Name = "Section_" + section.Id;
                    block.AddChild(UiKit.SectionTitle(Tr(section.TitleKey), UiTokens.FontBody));
                    var content = UiKit.Column(UiTokens.Space2);
                    content.Name = "Content";
                    block.AddChild(content);
                    sectionsBox.AddChild(block);
                    section.Build(content, id);
                }
                catch (Exception ex)
                {
                    GameLogger.Log("STATS", $"sheet section '{section.Id}' failed: {ex.Message}", LogLevel.Error);
                }
            }
        }

        private void ResolveNodes()
        {
            const string root = "Window/Margin/Root";
            const string main = root + "/Body/Scroll/Main";
            backdrop = GetNodeOrNull<ColorRect>("Bg");
            window = GetNodeOrNull<Panel>("Window");
            rule = GetNodeOrNull<Panel>(root + "/Rule");
            title = GetNodeOrNull<Label>(root + "/Header/Title");
            closeButton = GetNodeOrNull<Button>(root + "/Header/CloseButton");
            partyLabel = GetNodeOrNull<Label>(root + "/Body/Party/PartyLabel");
            partyList = GetNodeOrNull<VBoxContainer>(root + "/Body/Party/PartyList");
            portrait = GetNodeOrNull<TextureRect>(main + "/Hero/Portrait");
            nameLabel = GetNodeOrNull<Label>(main + "/Hero/Info/Name");
            archetypeLabel = GetNodeOrNull<Label>(main + "/Hero/Info/Archetype");
            levelLabel = GetNodeOrNull<Label>(main + "/Hero/Info/Level");
            expBar = GetNodeOrNull<ProgressBar>(main + "/Hero/Info/ExpBar");
            archetypeHint = GetNodeOrNull<Label>(main + "/Hero/Info/ArchetypeHint");
            statsBox = GetNodeOrNull<VBoxContainer>(main + "/Stats");
            sectionsBox = GetNodeOrNull<VBoxContainer>(main + "/Sections");
            emptyLabel = GetNodeOrNull<Label>(main + "/Empty");
        }

        private void ApplyStyles()
        {
            if (backdrop != null)
                UiKit.ApplyPanelBackdrop(backdrop);
            window?.AddThemeStyleboxOverride("panel", UiKit.Frame(UiTokens.RadiusLg, UiTokens.Surface0, UiTokens.Line));
            rule?.AddThemeStyleboxOverride("panel", UiKit.Frame(0, UiTokens.Line));
            Style(title, UiTokens.FontTitle, UiTokens.InkBright);
            if (title != null)
            {
                title.Uppercase = true;
                title.Text = Tr("stats/sheet/title");
            }
            Style(partyLabel, UiTokens.FontTiny, UiTokens.Faint);
            if (partyLabel != null)
            {
                partyLabel.Uppercase = true;
                partyLabel.Text = Tr("stats/sheet/party");
            }
            Style(nameLabel, UiTokens.FontH3, UiTokens.InkBright);
            Style(archetypeLabel, UiTokens.FontBodyLarge, UiTokens.AccentBright);
            Style(levelLabel, UiTokens.FontBody, UiTokens.Body);
            Style(archetypeHint, UiTokens.FontBodySmall, UiTokens.Muted);
            Style(emptyLabel, UiTokens.FontBodyLarge, UiTokens.Faint);
            if (portrait != null)
                portrait.TextureFilter = CanvasItem.TextureFilterEnum.Nearest;
            if (expBar != null)
            {
                expBar.AddThemeStyleboxOverride("background", UiKit.Frame(UiTokens.RadiusXs, UiTokens.Well, UiTokens.LineSoft));
                expBar.AddThemeStyleboxOverride("fill", UiKit.Frame(UiTokens.RadiusXs, UiTokens.Accent));
            }
            if (closeButton != null)
            {
                closeButton.TooltipText = Tr("stats/sheet/close");
                CthangoverTheme.ApplyIcon(closeButton);
            }
        }

        private static string NameOf(string id)
        {
            var key = StatsService.Template(id)?.Name;
            return string.IsNullOrEmpty(key) ? id : Tr(key);
        }

        private static void Style(Label label, int size, Color color)
        {
            if (label == null)
                return;
            label.AddThemeFontSizeOverride("font_size", size);
            label.AddThemeColorOverride("font_color", color);
        }

        private static void Clear(Node box)
        {
            if (box == null)
                return;
            foreach (var child in box.GetChildren())
            {
                box.RemoveChild(child);
                child.QueueFree();
            }
        }

        private static string Tr(string key) => TranslationServer.Translate(key).ToString();
    }
}
