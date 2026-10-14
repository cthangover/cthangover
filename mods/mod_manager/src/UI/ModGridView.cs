using System;
using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI.Theming;
using Cthangover.Core.Utils;
using Godot;

using Cthangover.Mods.Interface.Theming;
namespace Mods.ModManager
{
    /// <summary>
    /// Scrollable grid of <see cref="ModCard"/> tiles, composed of
    /// <see cref="ModGridGroup"/> sections (see
    /// <c>scenes/components/mod_grid_view.tscn</c>). Cards flow inside
    /// <see cref="HFlowContainer"/>s, so the number of columns adapts to the
    /// viewport width — the Godot answer to Nexus's responsive auto-fill grid.
    /// In grouping mode one section per category is created with a header; in flat
    /// mode a single header-less section is used.
    /// </summary>
    public partial class ModGridView : ScrollContainer
    {
        private const string GroupScene = "scenes/components/mod_grid_group.tscn";
        private const string CardScene = "scenes/components/mod_card.tscn";

        private VBoxContainer _host;

        /// <summary>Raised when a card body is clicked.</summary>
        public event Action<ModCardData> CardClicked;

        /// <summary>Raised when a card's action button is pressed.</summary>
        public event Action<ModCardData> ActionPressed;

        /// <summary>Raised when a card's enable switch is flipped.</summary>
        public event Action<ModCardData> EnabledToggled;

        /// <summary>Raised after a card's version selection changes.</summary>
        public event Action<ModCardData> VersionChanged;

        /// <summary>Raised when any card's "in N collections" cross-link is pressed.</summary>
        public event Action CollectionsLinkRequested;

        /// <summary>Rebuilds the grid from scratch; called on every filter change.</summary>
        public void SetItems(IReadOnlyList<ModCardData> items, bool groupByCategory)
        {
            if (_host == null)
                _host = GetNode<VBoxContainer>("Host");

            foreach (var child in _host.GetChildren())
                child.QueueFree();

            if (items == null || items.Count == 0)
            {
                _host.AddChild(BuildEmptyLabel());
                return;
            }

            if (!groupByCategory)
            {
                FillGroup(AddGroup(null), items);
                return;
            }

            foreach (var category in ModCategoryNames.Ordered)
            {
                var group = new List<ModCardData>();
                foreach (var item in items)
                {
                    if (ModCategoryNames.Primary(item.Category) == category)
                        group.Add(item);
                }

                if (group.Count == 0)
                    continue;

                var header = $"{ModCategoryNames.Translate(category)}  ·  {group.Count}";
                FillGroup(AddGroup(header), group);
            }
        }

        private ModGridGroup AddGroup(string header)
        {
            var group = TscnScenes.LoadAndBuild(GroupScene) as ModGridGroup;
            if (group == null)
            {
                GameLogger.Log("MOD_MANAGER", $"Grid: failed to build '{GroupScene}'", LogLevel.Error);
                return null;
            }

            group.SetHeader(header);
            _host.AddChild(group);
            return group;
        }

        private void FillGroup(ModGridGroup group, IReadOnlyList<ModCardData> items)
        {
            if (group == null)
                return;

            foreach (var data in items)
            {
                var card = TscnScenes.LoadAndBuild(CardScene) as ModCard;
                if (card == null)
                {
                    GameLogger.Log("MOD_MANAGER", $"Grid: failed to build '{CardScene}'", LogLevel.Error);
                    continue;
                }

                group.Flow.AddChild(card);
                card.Clicked += OnCardClicked;
                card.ActionPressed += OnCardActionPressed;
                card.EnabledToggled += OnCardEnabledToggled;
                card.VersionChanged += OnCardVersionChanged;
                card.CollectionsLinkRequested += () => CollectionsLinkRequested?.Invoke();
                card.Init(data);
            }
        }

        private static Label BuildEmptyLabel()
        {
            return UiKit.EmptyState(TranslationServer.Translate("mod_manager/no_results"));
        }

        private void OnCardClicked(ModCardData data) => CardClicked?.Invoke(data);

        private void OnCardActionPressed(ModCardData data) => ActionPressed?.Invoke(data);

        private void OnCardEnabledToggled(ModCardData data) => EnabledToggled?.Invoke(data);

        private void OnCardVersionChanged(ModCardData data) => VersionChanged?.Invoke(data);
    }
}
