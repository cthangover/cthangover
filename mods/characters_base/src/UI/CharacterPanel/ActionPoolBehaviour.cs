using System.Collections.Generic;
using Cthangover.Core.Characters;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.UI.Theming;
using Godot;

using Cthangover.Mods.Interface.Theming;
namespace Cthangover.Core.UI.CharacterPanel
{
    /// <summary>
    /// Right-hand column of the character panel: the player's persistent
    /// collection of unassigned action cards. Shows exactly what is stored in
    /// <see cref="Settings.ActionPoolData.ActionIds"/> — no filtering by
    /// assigned slots, duplicates allowed — and layers display-only search and
    /// type filters on top. The static layout (header, search box, filter tab
    /// row, scroll list, footer hint) lives in
    /// <c>scenes/ui/action_pool.tscn</c>; the panel hosts the scene instance
    /// and this behaviour resolves its nodes. Cards are rebuilt from
    /// <see cref="ActionCharacterFactory"/> on every refresh, so the pool is a
    /// pure projection of save data; drag orchestration stays in
    /// <see cref="CharacterPanelBehaviour"/>.
    /// </summary>
    public partial class ActionPoolBehaviour : Control
    {
        private static readonly StyleBoxFlat InputStyle = MakeInputStyle(false);
        private static readonly StyleBoxFlat InputFocusStyle = MakeInputStyle(true);

        private readonly List<string> _ids = new();
        private readonly List<ActionCardBehaviour> _cards = new();
        private ActionCharacterType? _filter;
        private string _search = string.Empty;

        private Label _countLabel;
        private Label _title;
        private LineEdit _searchEdit;
        private HFlowContainer _tabsRow;
        private ScrollContainer _scroll;
        private VBoxContainer _list;
        private Label _footer;

        /// <summary>Cards currently visible in the list, in list order.</summary>
        public IReadOnlyList<ActionCardBehaviour> VisibleCards => _cards;

        /// <summary>
        /// Repopulates the pool from the persisted ID list. Every ID gets a
        /// card, including duplicates; unknown IDs are skipped silently.
        /// Search/filter state is preserved.
        /// </summary>
        public void Refresh(List<string> poolActionIds)
        {
            _ids.Clear();
            if (poolActionIds != null)
                _ids.AddRange(poolActionIds);
            Rebuild();
        }

        /// <summary>Accepts a dragged card back into the pool; the data list is updated by the panel.</summary>
        public void AcceptCard(ActionCardBehaviour card)
        {
            if (card == null)
                return;

            card.CurrentSlot?.RemoveCard();
            card.CurrentSlot = null;
            card.SetSelected(false);
            card.SetCompact(true);

            if (card.GetParent() != _list)
            {
                card.GetParent()?.RemoveChild(card);
                _list.AddChild(card);
            }

            card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _cards.Add(card);
        }

        /// <summary>Detaches a card from the pool visuals without touching save data.</summary>
        public void RemoveCard(ActionCardBehaviour card)
        {
            if (card == null)
                return;
            _cards.Remove(card);
            card.GetParent()?.RemoveChild(card);
        }

        private void ResolveNodes()
        {
            _title ??= GetNodeOrNull<Label>("Root/Header/Title");
            _countLabel ??= GetNodeOrNull<Label>("Root/Header/CountLabel");
            _searchEdit ??= GetNodeOrNull<LineEdit>("Root/SearchEdit");
            _tabsRow ??= GetNodeOrNull<HFlowContainer>("Root/TabsRow");
            _scroll ??= GetNodeOrNull<ScrollContainer>("Root/Scroll");
            _list ??= GetNodeOrNull<VBoxContainer>("Root/Scroll/List");
            _footer ??= GetNodeOrNull<Label>("Root/Footer");

            if (_searchEdit == null)
                return;

            if (string.IsNullOrEmpty(_searchEdit.PlaceholderText))
            {
                _searchEdit.PlaceholderText = ActionCharacterUi.Translate("ui/character_panel/search");
                _searchEdit.AddThemeFontSizeOverride("font_size", 12);
                _searchEdit.AddThemeStyleboxOverride("normal", InputStyle);
                _searchEdit.AddThemeStyleboxOverride("focus", InputFocusStyle);
                _searchEdit.AddThemeColorOverride("font_color", UiTokens.Ink);
                _searchEdit.AddThemeColorOverride("font_placeholder_color", UiTokens.Faint);
                _searchEdit.AddThemeColorOverride("caret_color", UiTokens.Accent);
                _searchEdit.TextChanged += OnSearchChanged;
            }

            if (_title != null && string.IsNullOrEmpty(_title.Text))
            {
                _title.Text = ActionCharacterUi.Translate("ui/character_panel/pool_title").ToUpperInvariant();
                _title.AddThemeColorOverride("font_color", UiTokens.Ink);
                _title.AddThemeFontSizeOverride("font_size", 14);
            }

            if (_countLabel != null && string.IsNullOrEmpty(_countLabel.Text))
            {
                _countLabel.Text = "0";
                _countLabel.AddThemeColorOverride("font_color", UiTokens.Body);
                _countLabel.AddThemeFontSizeOverride("font_size", 11);
            }

            if (_footer != null && string.IsNullOrEmpty(_footer.Text))
            {
                _footer.Text = ActionCharacterUi.Translate("ui/character_panel/pool_hint");
                _footer.AddThemeColorOverride("font_color", UiTokens.Faint);
            }
        }

        private void Rebuild()
        {
            ResolveNodes();
            if (_list == null || _countLabel == null)
                return;

            foreach (var card in _cards)
                card.QueueFree();
            _cards.Clear();

            foreach (var id in _ids)
            {
                var action = ActionCharacterFactory.Instance.Get(id);
                if (action == null || !Matches(action))
                    continue;

                var card = CharacterPanelPrefabs.InstantiateCard();
                if (card == null)
                    continue;

                card.SetCompact(true);
                card.SetAction(action);
                card.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                _list.AddChild(card);
                _cards.Add(card);
            }

            if (_cards.Count == 0)
                _list.AddChild(BuildEmptyLabel());

            _countLabel.Text = _ids.Count.ToString();
            BuildTabs();
        }

        private Control BuildEmptyLabel()
        {
            var hasAny = _ids.Count > 0;
            var label = ActionCharacterUi.MakeLabel(
                ActionCharacterUi.Translate(hasAny
                    ? "ui/character_panel/nothing_found"
                    : "ui/character_panel/pool_empty"), 12, UiTokens.Faint);
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            label.CustomMinimumSize = new Vector2(0, 80);
            label.VerticalAlignment = VerticalAlignment.Center;
            return label;
        }

        private void BuildTabs()
        {
            if (_tabsRow == null)
                return;

            foreach (var child in _tabsRow.GetChildren())
                child.QueueFree();

            foreach (var (type, key) in ActionCharacterUi.Filters)
            {
                var button = new Button
                {
                    Text = ActionCharacterUi.Translate(key) + " " + CountByType(type),
                    ToggleMode = true,
                    ButtonPressed = type == _filter,
                    FocusMode = FocusModeEnum.None,
                };
                CthangoverTheme.ApplyTab(button);

                var captured = type;
                button.Pressed += () => OnFilterPressed(captured);
                _tabsRow.AddChild(button);
            }
        }

        private int CountByType(ActionCharacterType? type)
        {
            if (type == null)
                return _ids.Count;

            var count = 0;
            foreach (var id in _ids)
            {
                var action = ActionCharacterFactory.Instance.Get(id);
                if (action != null && action.Type == type)
                    count++;
            }
            return count;
        }

        private bool Matches(ActionCharacter action)
        {
            if (_filter != null && action.Type != _filter)
                return false;
            if (string.IsNullOrWhiteSpace(_search))
                return true;

            var needle = _search.Trim().ToLowerInvariant();
            if (ActionCharacterUi.Translate(action.Name).ToLowerInvariant().Contains(needle))
                return true;
            return action.ID != null && action.ID.ToLowerInvariant().Contains(needle);
        }

        private void OnFilterPressed(ActionCharacterType? type)
        {
            _filter = type;
            Rebuild();
        }

        private void OnSearchChanged(string text)
        {
            _search = text;
            Rebuild();
        }

        private static StyleBoxFlat MakeInputStyle(bool focus)
        {
            var style = UiKit.Frame(UiTokens.RadiusSm, UiTokens.Well,
                focus ? UiTokens.AccentLine : UiTokens.Line);
            style.ContentMarginLeft = 10;
            style.ContentMarginRight = 10;
            style.ContentMarginTop = 8;
            style.ContentMarginBottom = 8;
            return style;
        }
    }
}
