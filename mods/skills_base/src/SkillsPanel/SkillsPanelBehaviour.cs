using System;
using System.Collections.Generic;
using Cthangover.Core.Characters;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Core.Settings;
using Cthangover.Core.Skills;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.Utils;
using Godot;

using Cthangover.Mods.Interface.Theming;
namespace Cthangover.Mods.Skills
{
    /// <summary>
    /// Skills collection panel: the showcase grid of collected skill cards on
    /// the left, the study plate of the selected card on the right and the
    /// global "collection effect" plate hanging in the screen corner. The
    /// window structure lives in <c>skills_panel.tscn</c>; this behaviour only
    /// resolves the nodes, paints them with <see cref="UiTokens"/> and fills
    /// them with data.
    /// <para/>
    /// The grid never reserves slots for cards the player does not own: the
    /// total card count in the world depends on the loaded mods and is unknown
    /// at runtime, so only owned skills are shown and the header counter
    /// reports the collected amount. The effect plate sums every owned skill's
    /// <see cref="SkillEffectInfo"/> through <see cref="SkillEffectAggregator"/>,
    /// which is the "what the collection gives me right now" readout.
    /// <para/>
    /// Selecting a card only repaints the study plate; filters, search and sort
    /// rebuild the grid and keep the selection on the first visible card. All
    /// rarity colours come from <see cref="SkillPalette"/>, shared with the
    /// card scene, so a card and its plate can never disagree.
    /// </summary>
    public partial class SkillsPanelBehaviour : Widget
    {
        private enum SortMode
        {
            Rarity,
            Type,
            Name,
        }

        private const int Columns = 6;
        private const float CellGap = 10f;
        private const float SideWidth = 400f;
        private const float CardAspect = 210f / 160f;

        private readonly List<SkillCardBehaviour> _cards = new();
        private ModScenePrefab _cardPrefab;

        private ColorRect _backdrop;
        private Panel _window;
        private Label _title;
        private HBoxContainer _tabsRow;
        private Label _progressLabel;
        private Label _progressValue;
        private Button _closeButton;
        private Panel _rule;
        private LineEdit _searchEdit;
        private OptionButton _sortOption;
        private HFlowContainer _rarityTabs;
        private ScrollContainer _gridScroll;
        private Control _gridContent;
        private Label _emptyLabel;
        private Label _hint;
        private Button _resetButton;
        private PanelContainer _side;
        private Label _sideLabel;
        private Control _plate;
        private Panel _plateBg;
        private PanelContainer _plateArt;
        private TextureRect _plateImage;
        private Label _plateGems;
        private PanelContainer _medal;
        private Label _medalGlyph;
        private PanelContainer _plateNameBox;
        private Label _plateName;
        private HFlowContainer _badges;
        private Label _desc;
        private HFlowContainer _chips;
        private PanelContainer _grant;
        private TextureRect _grantIcon;
        private Label _grantCaption;
        private Label _grantName;
        private Label _passportEmpty;
        private Panel _hud;
        private Panel _hudAccent;
        private Label _hudTitle;
        private Label _hudCount;
        private Panel _hudRule;
        private ScrollContainer _hudScroll;
        private GridContainer _hudList;
        private Label _hudEmpty;
        private Label _hudFoot;

        private SkillInfo _selected;
        private SkillType? _typeFilter;
        private RareType? _rarityFilter;
        private SortMode _sort = SortMode.Rarity;
        private string _search = string.Empty;

        /// <summary>Resolves nodes, applies styles and texts, loads the skill card prefab and wires search/sort/close handlers.</summary>
        public override void _Ready()
        {
            base._Ready();
            ResolveNodes();
            ApplyStaticStyles();
            ApplyStaticTexts();

            _cardPrefab = new ModScenePrefab("scenes/ui/skills_panel/skill_card.tscn");

            if (_closeButton != null)
                _closeButton.Pressed += OnClosePressed;
            if (_searchEdit != null)
                _searchEdit.TextChanged += OnSearchChanged;
            if (_sortOption != null)
                _sortOption.ItemSelected += OnSortSelected;
            if (_resetButton != null)
                _resetButton.Pressed += OnResetPressed;

            GameLogger.Log("SKILLSPANEL", $"Ready: grid={_gridContent != null} side={_side != null} hud={_hudList != null} prefab={_cardPrefab != null}");

            // A scene that starts visible never goes through Widget.Show (the
            // lifecycle only fires on the typed Show/Switch calls), so build the
            // panel here too — ShowConstruct rebuilds from scratch, and the next
            // open just repeats that rebuild.
            if (Visible)
            {
                EnsureConstructed();
                ShowConstruct();
            }
        }

        /// <summary>
        /// Rebuilds the whole panel on every open with a clean filter state, so
        /// the player always lands on the full collection. Newly collected
        /// skills appear here because the grid is rebuilt from
        /// <see cref="SkillData.Skills"/> rather than cached.
        /// </summary>
        protected override void ShowConstruct()
        {
            _typeFilter = null;
            _rarityFilter = null;
            _sort = SortMode.Rarity;
            _search = string.Empty;

            if (_searchEdit != null)
                _searchEdit.Text = string.Empty;
            if (_sortOption != null)
                _sortOption.Selected = 0;

            var owned = OwnedSkills();
            RebuildTypeTabs(owned);
            RebuildRarityTabs(owned);
            RebuildGrid(owned);
            RebuildHud(owned);

            GameLogger.Log("SKILLSPANEL", $"ShowConstruct: {owned.Count} skill(s) owned");
        }

        protected override void HideDestruct()
        {
            ClearCards();
            _selected = null;
            ClearChildren(_badges);
            ClearChildren(_chips);
        }

        private void ResolveNodes()
        {
            const string root = "Window/Margin/Root";
            _backdrop = GetNodeOrNull<ColorRect>("Bg");
            _window = GetNodeOrNull<Panel>("Window");
            _title = GetNodeOrNull<Label>(root + "/Header/Title");
            _tabsRow = GetNodeOrNull<HBoxContainer>(root + "/Header/TabsRow");
            _progressLabel = GetNodeOrNull<Label>(root + "/Header/ProgressLabel");
            _progressValue = GetNodeOrNull<Label>(root + "/Header/ProgressValue");
            _closeButton = GetNodeOrNull<Button>(root + "/Header/CloseButton");
            _rule = GetNodeOrNull<Panel>(root + "/Rule");
            _searchEdit = GetNodeOrNull<LineEdit>(root + "/Body/Showcase/Toolbar/SearchEdit");
            _sortOption = GetNodeOrNull<OptionButton>(root + "/Body/Showcase/Toolbar/SortOption");
            _rarityTabs = GetNodeOrNull<HFlowContainer>(root + "/Body/Showcase/RarityTabs");
            _gridScroll = GetNodeOrNull<ScrollContainer>(root + "/Body/Showcase/GridScroll");
            _gridContent = GetNodeOrNull<Control>(root + "/Body/Showcase/GridScroll/GridContent");
            _emptyLabel = GetNodeOrNull<Label>(root + "/Body/Showcase/EmptyLabel");
            _hint = GetNodeOrNull<Label>(root + "/Body/Showcase/Footer/Hint");
            _resetButton = GetNodeOrNull<Button>(root + "/Body/Showcase/Footer/ResetButton");
            _side = GetNodeOrNull<PanelContainer>(root + "/Body/Side");
            _sideLabel = GetNodeOrNull<Label>(root + "/Body/Side/SideScroll/Passport/SideLabel");
            _plate = GetNodeOrNull<Control>(root + "/Body/Side/SideScroll/Passport/Plate");
            _plateBg = GetNodeOrNull<Panel>(root + "/Body/Side/SideScroll/Passport/Plate/PlateBg");
            _plateArt = GetNodeOrNull<PanelContainer>(root + "/Body/Side/SideScroll/Passport/Plate/PlateArt");
            _plateImage = GetNodeOrNull<TextureRect>(root + "/Body/Side/SideScroll/Passport/Plate/PlateArt/PlateImage");
            _plateGems = GetNodeOrNull<Label>(root + "/Body/Side/SideScroll/Passport/Plate/PlateGems");
            _medal = GetNodeOrNull<PanelContainer>(root + "/Body/Side/SideScroll/Passport/Plate/PlateMedal");
            _medalGlyph = GetNodeOrNull<Label>(root + "/Body/Side/SideScroll/Passport/Plate/PlateMedal/MedalGlyph");
            _plateNameBox = GetNodeOrNull<PanelContainer>(root + "/Body/Side/SideScroll/Passport/Plate/PlateNameBox");
            _plateName = GetNodeOrNull<Label>(root + "/Body/Side/SideScroll/Passport/Plate/PlateNameBox/PlateName");
            _badges = GetNodeOrNull<HFlowContainer>(root + "/Body/Side/SideScroll/Passport/Badges");
            _desc = GetNodeOrNull<Label>(root + "/Body/Side/SideScroll/Passport/Desc");
            _chips = GetNodeOrNull<HFlowContainer>(root + "/Body/Side/SideScroll/Passport/Chips");
            _grant = GetNodeOrNull<PanelContainer>(root + "/Body/Side/SideScroll/Passport/Grant");
            _grantIcon = GetNodeOrNull<TextureRect>(root + "/Body/Side/SideScroll/Passport/Grant/GrantRow/GrantIcon");
            _grantCaption = GetNodeOrNull<Label>(root + "/Body/Side/SideScroll/Passport/Grant/GrantRow/GrantInfo/GrantCaption");
            _grantName = GetNodeOrNull<Label>(root + "/Body/Side/SideScroll/Passport/Grant/GrantRow/GrantInfo/GrantName");
            _passportEmpty = GetNodeOrNull<Label>(root + "/Body/Side/SideScroll/Passport/Empty");
            _hud = GetNodeOrNull<Panel>("Hud");
            _hudAccent = GetNodeOrNull<Panel>("Hud/HudAccent");
            _hudTitle = GetNodeOrNull<Label>("Hud/HudMargin/HudColumn/HudHead/HudTitle");
            _hudCount = GetNodeOrNull<Label>("Hud/HudMargin/HudColumn/HudHead/HudCount");
            _hudRule = GetNodeOrNull<Panel>("Hud/HudMargin/HudColumn/HudRule");
            _hudScroll = GetNodeOrNull<ScrollContainer>("Hud/HudMargin/HudColumn/HudScroll");
            _hudList = GetNodeOrNull<GridContainer>("Hud/HudMargin/HudColumn/HudScroll/HudList");
            _hudEmpty = GetNodeOrNull<Label>("Hud/HudMargin/HudColumn/HudEmpty");
            _hudFoot = GetNodeOrNull<Label>("Hud/HudMargin/HudColumn/HudFoot");
        }

        private void ApplyStaticStyles()
        {
            if (_backdrop != null)
                UiKit.ApplyPanelBackdrop(_backdrop);
            if (_window != null)
                _window.AddThemeStyleboxOverride("panel", MakePanelStyle(UiTokens.Surface0, UiTokens.Line, UiTokens.RadiusLg, true));
            if (_rule != null)
                _rule.AddThemeStyleboxOverride("panel", MakePanelStyle(UiTokens.Line, UiTokens.Line, 0));
            if (_side != null)
                _side.AddThemeStyleboxOverride("panel", MakeSideStyle());
            if (_plateBg != null)
                _plateBg.AddThemeStyleboxOverride("panel", MakePanelStyle(UiTokens.Surface1, UiTokens.Line, UiTokens.RadiusMd));
            if (_plateArt != null)
                _plateArt.AddThemeStyleboxOverride("panel", MakePanelStyle(UiTokens.Well, UiTokens.Line, UiTokens.RadiusSm));
            if (_grant != null)
            {
                var grantStyle = MakePanelStyle(UiTokens.Well, UiTokens.Line, UiTokens.RadiusMd);
                ApplyContentMargins(grantStyle, 10, 9);
                _grant.AddThemeStyleboxOverride("panel", grantStyle);
            }
            if (_hud != null)
                _hud.AddThemeStyleboxOverride("panel", MakePanelStyle(UiTokens.Surface0, UiTokens.Line, UiTokens.RadiusLg, true));
            if (_hudAccent != null)
                _hudAccent.AddThemeStyleboxOverride("panel", UiKit.Frame(0, UiTokens.AccentLine));
            if (_hudRule != null)
                _hudRule.AddThemeStyleboxOverride("panel", MakePanelStyle(UiTokens.LineSoft, UiTokens.LineSoft, 0));

            StyleLabel(_title, UiTokens.FontTitle, UiTokens.InkBright);
            if (_title != null)
                _title.Uppercase = true;
            StyleLabel(_progressLabel, UiTokens.FontTiny, UiTokens.Faint);
            if (_progressLabel != null)
                _progressLabel.Uppercase = true;
            StyleLabel(_progressValue, UiTokens.FontBodyLarge, UiTokens.AccentBright);
            StyleLabel(_hint, UiTokens.FontBodySmall, UiTokens.Faint);
            StyleLabel(_sideLabel, UiTokens.FontTiny, UiTokens.Faint);
            if (_sideLabel != null)
                _sideLabel.Uppercase = true;
            StyleLabel(_plateGems, UiTokens.FontSmall, UiTokens.Muted);
            StyleLabel(_medalGlyph, UiTokens.FontBodyLarge, UiTokens.Muted);
            StyleLabel(_plateName, UiTokens.FontBodyLarge, UiTokens.InkBright);
            StyleLabel(_desc, UiTokens.FontBody, UiTokens.Body);
            StyleLabel(_grantCaption, UiTokens.FontTiny, UiTokens.Faint);
            if (_grantCaption != null)
                _grantCaption.Uppercase = true;
            StyleLabel(_grantName, UiTokens.FontBodyLarge, UiTokens.InkBright);
            StyleLabel(_passportEmpty, UiTokens.FontBodyLarge, UiTokens.Faint);
            StyleLabel(_hudTitle, UiTokens.FontTiny, UiTokens.AccentDim);
            if (_hudTitle != null)
                _hudTitle.Uppercase = true;
            StyleLabel(_hudCount, UiTokens.FontSmall, UiTokens.Muted);
            StyleLabel(_hudEmpty, UiTokens.FontBodySmall, UiTokens.Faint);
            StyleLabel(_hudFoot, UiTokens.FontTiny, UiTokens.Faint);

            if (_closeButton != null)
            {
                _closeButton.Text = "✕";
                _closeButton.TooltipText = Translate("ui/journal/close");
                CthangoverTheme.ApplyIcon(_closeButton);
            }

            if (_resetButton != null)
            {
                _resetButton.FocusMode = FocusModeEnum.None;
                _resetButton.AddThemeFontSizeOverride("font_size", UiTokens.FontBody);
            }

            if (_medal != null)
                _medal.AddThemeStyleboxOverride("panel", MakeCircleStyle(UiTokens.Line));
            if (_plateNameBox != null)
                _plateNameBox.AddThemeStyleboxOverride("panel", MakeNamePlateStyle(UiTokens.Line));
        }

        private void ApplyStaticTexts()
        {
            if (_title != null)
                _title.Text = Translate("ui/skills/title");
            if (_progressLabel != null)
                _progressLabel.Text = Translate("ui/skills/collection");
            if (_hint != null)
                _hint.Text = Translate("ui/skills/hint");
            if (_resetButton != null)
                _resetButton.Text = Translate("ui/skills/reset");
            if (_sideLabel != null)
                _sideLabel.Text = Translate("ui/skills/side_title");
            if (_hudTitle != null)
                _hudTitle.Text = Translate("ui/skills/collection_effect");
            if (_hudFoot != null)
                _hudFoot.Text = Translate("ui/skills/collection_effect_hint");
            if (_hudEmpty != null)
                _hudEmpty.Text = Translate("ui/skills/no_effects");
            if (_grantCaption != null)
                _grantCaption.Text = Translate("ui/skills/grants_action");

            if (_searchEdit != null)
                _searchEdit.PlaceholderText = Translate("ui/skills/search");
            if (_sortOption != null)
            {
                _sortOption.Clear();
                _sortOption.AddItem(Translate("ui/skills/sort_rarity"), (int)SortMode.Rarity);
                _sortOption.AddItem(Translate("ui/skills/sort_type"), (int)SortMode.Type);
                _sortOption.AddItem(Translate("ui/skills/sort_name"), (int)SortMode.Name);
                _sortOption.Selected = 0;
            }
        }

        private List<SkillInfo> OwnedSkills()
        {
            var result = new List<SkillInfo>();
            foreach (var id in GameData.Instance.Runtime.SkillData.Skills)
            {
                var skill = SkillFactory.Instance.Get(id);
                if (skill != null)
                    result.Add(skill);
            }
            return result;
        }

        private void RebuildTypeTabs(List<SkillInfo> owned)
        {
            ClearChildren(_tabsRow);
            if (_tabsRow == null)
                return;

            AddTypeTab(null, "ui/skills/filter_all", owned.Count);
            AddTypeTab(SkillType.Passive, SkillPalette.TypeLocaleKey(SkillType.Passive), CountType(owned, SkillType.Passive));
            AddTypeTab(SkillType.Active, SkillPalette.TypeLocaleKey(SkillType.Active), CountType(owned, SkillType.Active));
            AddTypeTab(SkillType.ForCharacter, SkillPalette.TypeLocaleKey(SkillType.ForCharacter), CountType(owned, SkillType.ForCharacter));
        }

        private void AddTypeTab(SkillType? type, string localeKey, int count)
        {
            var active = _typeFilter == type;
            var button = new Button
            {
                Text = Translate(localeKey) + " " + count,
                FocusMode = FocusModeEnum.None,
            };
            button.AddThemeFontSizeOverride("font_size", UiTokens.FontBody);
            button.AddThemeStyleboxOverride("normal", MakeTypeTabStyle(active, false));
            button.AddThemeStyleboxOverride("hover", MakeTypeTabStyle(active, true));
            button.AddThemeStyleboxOverride("pressed", MakeTypeTabStyle(active, true));
            button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            button.AddThemeColorOverride("font_color", active ? UiTokens.AccentBright : UiTokens.Muted);
            button.AddThemeColorOverride("font_hover_color", active ? UiTokens.AccentBright : UiTokens.Ink);
            button.AddThemeColorOverride("font_pressed_color", UiTokens.AccentBright);
            button.Pressed += () => OnTypePressed(type);
            _tabsRow.AddChild(button);
        }

        private void RebuildRarityTabs(List<SkillInfo> owned)
        {
            ClearChildren(_rarityTabs);
            if (_rarityTabs == null)
                return;

            AddRarityTab(null, "ui/skills/filter_rarities", owned.Count, UiTokens.Accent, false);
            foreach (RareType rarity in Enum.GetValues(typeof(RareType)))
                AddRarityTab(rarity, SkillPalette.RarityLocaleKey(rarity), CountRarity(owned, rarity), SkillPalette.RarityColor(rarity), true);
        }

        private void AddRarityTab(RareType? rarity, string localeKey, int count, Color color, bool withGlyph)
        {
            var active = _rarityFilter == rarity;
            var button = new Button
            {
                Text = (withGlyph ? "◆ " : string.Empty) + Translate(localeKey) + " " + count,
                FocusMode = FocusModeEnum.None,
            };
            button.AddThemeFontSizeOverride("font_size", UiTokens.FontSmall);
            button.AddThemeStyleboxOverride("normal", MakeRarityTabStyle(active, false, color));
            button.AddThemeStyleboxOverride("hover", MakeRarityTabStyle(active, true, color));
            button.AddThemeStyleboxOverride("pressed", MakeRarityTabStyle(active, true, color));
            button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            button.AddThemeColorOverride("font_color", active ? color : UiTokens.Alpha(color, 0.75f));
            button.AddThemeColorOverride("font_hover_color", color);
            button.AddThemeColorOverride("font_pressed_color", color);
            button.Pressed += () => OnRarityPressed(rarity);
            _rarityTabs.AddChild(button);
        }

        private void RebuildGrid(List<SkillInfo> owned)
        {
            var previous = _selected;
            ClearCards();

            if (_gridContent == null)
                return;

            var skills = FilterAndSort(owned);
            if (skills.Count == 0)
            {
                if (_gridScroll != null)
                    _gridScroll.Visible = false;
                if (_emptyLabel != null)
                {
                    _emptyLabel.Visible = true;
                    _emptyLabel.Text = Translate(owned.Count == 0 ? "ui/skills/empty" : "ui/skills/nothing_found");
                }
                HidePassport(owned.Count == 0 ? "ui/skills/empty" : "ui/skills/nothing_found");
                GameLogger.Log("SKILLSPANEL", $"RebuildGrid: no cards ({owned.Count} owned)");
                return;
            }

            if (_gridScroll != null)
                _gridScroll.Visible = true;
            if (_emptyLabel != null)
                _emptyLabel.Visible = false;

            var cardSize = CalculateCardSize();
            for (var i = 0; i < skills.Count; i++)
            {
                var skill = skills[i];
                var card = _cardPrefab?.Instantiate<SkillCardBehaviour>();
                if (card == null)
                    continue;

                _gridContent.AddChild(card);
                card.Construct(skill);
                PlaceCard(card, i, cardSize);

                card.GuiInput += @event =>
                {
                    if (@event is InputEventMouseButton mouse && mouse.Pressed && mouse.ButtonIndex == MouseButton.Left)
                        SelectSkill(skill);
                };

                _cards.Add(card);
            }

            if (_cards.Count == 0)
            {
                HidePassport("ui/skills/nothing_found");
                return;
            }

            var last = _cards[_cards.Count - 1];
            _gridContent.CustomMinimumSize = new Vector2(0, last.Position.Y + last.Size.Y + CellGap);

            var keep = previous == null ? null : skills.Find(candidate => candidate.ID == previous.ID);
            SelectSkill(keep ?? _cards[0].Model);

            GameLogger.Log("SKILLSPANEL", $"RebuildGrid: {_cards.Count} card(s) from {owned.Count} owned");
        }

        private List<SkillInfo> FilterAndSort(List<SkillInfo> owned)
        {
            var result = new List<SkillInfo>();
            foreach (var skill in owned)
            {
                if (_typeFilter.HasValue && skill.SkillType != _typeFilter.Value)
                    continue;
                if (_rarityFilter.HasValue && skill.RareType != _rarityFilter.Value)
                    continue;
                if (!MatchesSearch(skill))
                    continue;
                result.Add(skill);
            }
            result.Sort(CompareSkills);
            return result;
        }

        private bool MatchesSearch(SkillInfo skill)
        {
            if (string.IsNullOrWhiteSpace(_search))
                return true;

            var needle = _search.Trim().ToLowerInvariant();
            if (Translate(skill.Name).ToLowerInvariant().Contains(needle))
                return true;
            return skill.ID != null && skill.ID.ToLowerInvariant().Contains(needle);
        }

        private int CompareSkills(SkillInfo a, SkillInfo b)
        {
            switch (_sort)
            {
                case SortMode.Name:
                    return string.Compare(Translate(a.Name), Translate(b.Name), StringComparison.OrdinalIgnoreCase);
                case SortMode.Type:
                {
                    var type = ((int)a.SkillType).CompareTo((int)b.SkillType);
                    return type != 0 ? type : string.Compare(Translate(a.Name), Translate(b.Name), StringComparison.OrdinalIgnoreCase);
                }
                default:
                {
                    var rarity = ((int)b.RareType).CompareTo((int)a.RareType);
                    return rarity != 0 ? rarity : string.Compare(Translate(a.Name), Translate(b.Name), StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        private Vector2 CalculateCardSize()
        {
            var viewportWidth = GetViewportRect().Size.X;
            if (viewportWidth <= 0f)
                viewportWidth = 1920f;

            var panelWidth = viewportWidth - 80f - 48f;
            var showcaseWidth = Mathf.Max(320f, panelWidth - SideWidth - 14f);
            var cardWidth = (showcaseWidth - (Columns - 1) * CellGap) / Columns;
            cardWidth = Mathf.Clamp(cardWidth, 72f, 220f);
            return new Vector2(cardWidth, cardWidth * CardAspect);
        }

        private static void PlaceCard(SkillCardBehaviour card, int index, Vector2 size)
        {
            var column = index % Columns;
            var row = index / Columns;
            card.Position = new Vector2(column * (size.X + CellGap), row * (size.Y + CellGap));
            card.AnchorRight = 0f;
            card.AnchorBottom = 0f;
            card.Size = size;
        }

        private void SelectSkill(SkillInfo skill)
        {
            _selected = skill;
            foreach (var card in _cards)
                card.SetSelected(ReferenceEquals(card.Model, skill));
            BuildPassport(skill);
        }

        private void BuildPassport(SkillInfo skill)
        {
            if (skill == null)
            {
                HidePassport("ui/skills/nothing_found");
                return;
            }

            var rarityColor = SkillPalette.RarityColor(skill.RareType);
            var rarityLine = UiTokens.Alpha(rarityColor, 0.5f);

            SetPassportBlockVisible(true);
            if (_passportEmpty != null)
                _passportEmpty.Visible = false;

            if (_plateBg != null)
                _plateBg.AddThemeStyleboxOverride("panel", MakePanelStyle(UiTokens.Surface1, rarityColor, UiTokens.RadiusMd, true));
            if (_plateImage != null)
                _plateImage.Texture = skill.Sprite;
            if (_plateGems != null)
            {
                _plateGems.Text = new string('◆', Mathf.Clamp((int)skill.RareType + 1, 1, 5));
                _plateGems.AddThemeColorOverride("font_color", rarityColor);
            }
            if (_medal != null)
                _medal.AddThemeStyleboxOverride("panel", MakeCircleStyle(rarityColor));
            if (_medalGlyph != null)
                _medalGlyph.AddThemeColorOverride("font_color", rarityColor);
            if (_plateName != null)
                _plateName.Text = Translate(skill.Name);
            if (_plateNameBox != null)
                _plateNameBox.AddThemeStyleboxOverride("panel", MakeNamePlateStyle(rarityLine));

            ClearChildren(_badges);
            if (_badges != null)
            {
                _badges.AddChild(UiKit.Badge(Translate(SkillPalette.RarityLocaleKey(skill.RareType)), rarityColor));
                _badges.AddChild(UiKit.Badge(Translate(SkillPalette.TypeLocaleKey(skill.SkillType)), SkillPalette.TypeColor(skill.SkillType)));
            }

            if (_desc != null)
                _desc.Text = Translate(skill.Description);

            ClearChildren(_chips);
            if (_chips != null && skill.Effects != null)
            {
                foreach (var effect in skill.Effects)
                {
                    if (effect == null || string.IsNullOrEmpty(effect.Key))
                        continue;
                    _chips.AddChild(UiKit.PropertyChip(Translate(effect.Key), effect.FormatValue()));
                }
            }

            ApplyGrant(skill.GrantsAction);
        }

        private void HidePassport(string messageKey)
        {
            SetPassportBlockVisible(false);
            if (_passportEmpty != null)
            {
                _passportEmpty.Visible = true;
                _passportEmpty.Text = Translate(messageKey);
            }
        }

        private void SetPassportBlockVisible(bool visible)
        {
            if (_plate != null)
                _plate.Visible = visible;
            if (_badges != null)
                _badges.Visible = visible;
            if (_desc != null)
                _desc.Visible = visible;
            if (_chips != null)
                _chips.Visible = visible;
            if (_grant != null && !visible)
                _grant.Visible = false;
        }

        /// <summary>
        /// Shows the action card a skill gifts to the player. Skills without a
        /// granted action (pure passives and gates) keep the block hidden, and
        /// a broken cross-mod reference degrades to a plain skill card instead
        /// of failing the whole study plate.
        /// </summary>
        private void ApplyGrant(string actionId)
        {
            var action = string.IsNullOrEmpty(actionId) ? null : ActionTemplateRegistry.Get(actionId);
            if (_grant != null)
                _grant.Visible = action != null;
            if (action == null)
                return;

            if (_grantIcon != null)
                _grantIcon.Texture = action.Image;
            if (_grantName != null)
                _grantName.Text = Translate(action.Name);
        }

        private void RebuildHud(List<SkillInfo> owned)
        {
            if (_hudCount != null)
                _hudCount.Text = owned.Count.ToString();
            if (_progressValue != null)
                _progressValue.Text = owned.Count.ToString();

            var totals = SkillEffectAggregator.Aggregate(owned);
            ClearChildren(_hudList);

            var hasTotals = totals.Count > 0;
            if (_hudScroll != null)
                _hudScroll.Visible = hasTotals;
            if (_hudEmpty != null)
                _hudEmpty.Visible = !hasTotals;

            if (_hudList == null)
                return;

            foreach (var total in totals)
            {
                var cell = UiKit.Row(10);
                var value = UiKit.Label(total.FormatValue(), UiTokens.FontBodyLarge, UiTokens.AccentBright);
                value.CustomMinimumSize = new Vector2(54, 0);
                value.HorizontalAlignment = HorizontalAlignment.Right;
                cell.AddChild(value);

                var key = UiKit.Label(Translate(total.Key), UiTokens.FontBodySmall, UiTokens.Body);
                key.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                key.ClipText = true;
                cell.AddChild(key);

                _hudList.AddChild(cell);
            }
        }

        private static int CountType(List<SkillInfo> skills, SkillType type)
        {
            var count = 0;
            foreach (var skill in skills)
            {
                if (skill.SkillType == type)
                    count++;
            }
            return count;
        }

        private static int CountRarity(List<SkillInfo> skills, RareType rarity)
        {
            var count = 0;
            foreach (var skill in skills)
            {
                if (skill.RareType == rarity)
                    count++;
            }
            return count;
        }

        private void ClearCards()
        {
            foreach (var card in _cards)
                card.QueueFree();
            _cards.Clear();
        }

        private void OnTypePressed(SkillType? type)
        {
            _typeFilter = type;
            var owned = OwnedSkills();
            RebuildTypeTabs(owned);
            RebuildGrid(owned);
        }

        private void OnRarityPressed(RareType? rarity)
        {
            _rarityFilter = rarity;
            var owned = OwnedSkills();
            RebuildRarityTabs(owned);
            RebuildGrid(owned);
        }

        private void OnSearchChanged(string text)
        {
            _search = text;
            RebuildGrid(OwnedSkills());
        }

        private void OnSortSelected(long index)
        {
            _sort = (SortMode)index;
            RebuildGrid(OwnedSkills());
        }

        private void OnResetPressed()
        {
            ShowConstruct();
        }

        private void OnClosePressed()
        {
            Hide();
        }

        private static string Translate(string key)
            => TranslationServer.Translate(key).ToString();

        private static void StyleLabel(Label label, int fontSize, Color color)
        {
            if (label == null)
                return;
            label.AddThemeFontSizeOverride("font_size", fontSize);
            label.AddThemeColorOverride("font_color", color);
        }

        private static void ApplyContentMargins(StyleBoxFlat style, int horizontal, int vertical)
        {
            style.ContentMarginLeft = horizontal;
            style.ContentMarginRight = horizontal;
            style.ContentMarginTop = vertical;
            style.ContentMarginBottom = vertical;
        }

        private static StyleBoxFlat MakePanelStyle(Color background, Color border, int radius, bool shadow = false)
        {
            var style = UiKit.Frame(radius, background, border);
            if (shadow)
            {
                style.ShadowColor = new Color(0f, 0f, 0f, 0.6f);
                style.ShadowSize = 18;
                style.ShadowOffset = new Vector2(0f, 8f);
            }
            return style;
        }

        private static StyleBoxFlat MakeSideStyle()
        {
            var style = new StyleBoxFlat
            {
                BgColor = UiTokens.SideBg,
                BorderColor = UiTokens.Line,
                BorderWidthLeft = 1,
                ContentMarginLeft = 20,
                ContentMarginRight = 18,
                ContentMarginTop = 16,
                ContentMarginBottom = 16,
            };
            return style;
        }

        private static StyleBoxFlat MakeCircleStyle(Color border)
        {
            var style = UiKit.Frame(17, UiTokens.Surface0, border);
            ApplyContentMargins(style, 5, 4);
            return style;
        }

        private static StyleBoxFlat MakeNamePlateStyle(Color border)
        {
            var style = UiKit.Frame(UiTokens.RadiusXs, UiTokens.SlotTagBg, border);
            ApplyContentMargins(style, 10, 4);
            return style;
        }

        private static StyleBoxFlat MakeTypeTabStyle(bool active, bool hover)
        {
            var style = new StyleBoxFlat();
            style.BgColor = active ? UiTokens.AccentSoft : hover ? UiTokens.WhiteTrace : Colors.Transparent;
            if (active)
            {
                style.BorderColor = UiTokens.AccentLine;
                style.BorderWidthLeft = 1;
                style.BorderWidthRight = 1;
                style.BorderWidthTop = 1;
                style.BorderWidthBottom = 1;
            }
            style.CornerRadiusTopLeft = UiTokens.RadiusSm;
            style.CornerRadiusTopRight = UiTokens.RadiusSm;
            style.CornerRadiusBottomLeft = UiTokens.RadiusSm;
            style.CornerRadiusBottomRight = UiTokens.RadiusSm;
            ApplyContentMargins(style, 13, 7);
            return style;
        }

        private static StyleBoxFlat MakeRarityTabStyle(bool active, bool hover, Color color)
        {
            var style = UiKit.Frame(UiTokens.RadiusXs, Colors.Transparent, Colors.Transparent);
            if (active)
            {
                style.BgColor = UiTokens.Alpha(color, 0.09f);
                style.BorderColor = UiTokens.Alpha(color, 0.5f);
            }
            else if (hover)
            {
                style.BgColor = UiTokens.WhiteTrace;
            }
            ApplyContentMargins(style, 9, 4);
            return style;
        }

        private static void ClearChildren(Node container)
        {
            if (container == null)
                return;
            foreach (var child in container.GetChildren())
            {
                if (child is Node node)
                {
                    container.RemoveChild(node);
                    node.QueueFree();
                }
            }
        }
    }
}
