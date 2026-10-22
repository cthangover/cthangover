using System;
using System.Collections.Generic;
using Cthangover.Core.Audio;
using Cthangover.Core.Characters;
using Cthangover.Core.Factories.Impls;
using Cthangover.Core.Items;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Theming;
using Godot;

using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Mods.Items;
using Cthangover.Mods.Interface.Theming;
namespace Cthangover.Core.UI.Inventory
{
    /// <summary>
    /// Full-screen inventory panel following mockup 1: a header with type
    /// tabs and battle action-point pips, a search/sort toolbar, a six-column
    /// item grid on the left and an "item passport" on the right. The passport
    /// shows the translated name, type badges, description, a stats table, the
    /// mod-defined <c>PropertyData</c> bag and the contextual actions for the
    /// selected item (use/apply/learn/drop) with a quantity slider.
    ///
    /// Affordances are centralised in <see cref="InventoryItemUi"/> so the grid
    /// cell, the passport buttons and the target list never disagree. Actions
    /// that fail (<c>UseAction</c> returning <c>false</c>) do not consume the
    /// item and surface the reason in the passport message area instead.
    /// During battle every use/apply/drop costs one action point from the
    /// active character; outside battle actions are free.
    ///
    /// The grid rebuilds on every <see cref="IInventory.Change"/>, preserving
    /// the selected item by ID when it still exists.
    /// </summary>
    public partial class InventoryPanelBehaviour : Widget
    {
        private enum BagFilter
        {
            All,
            Food,
            Resource,
            Recipe,
            Quest,
        }

        private enum SortMode
        {
            Type,
            Name,
            Cost,
            Count,
        }

        private enum MessageKind
        {
            None,
            Ok,
            Error,
        }

        private const int Columns = 6;
        private const float CellGap = 10f;
        private const float PassportWidth = 430f;

        private static readonly (BagFilter Filter, string Key)[] FilterTabs =
        {
            (BagFilter.All, "ui/inventory/tab_all"),
            (BagFilter.Food, "ui/inventory/tab_food"),
            (BagFilter.Resource, "ui/inventory/tab_resource"),
            (BagFilter.Recipe, "ui/inventory/tab_recipe"),
            (BagFilter.Quest, "ui/inventory/tab_quest"),
        };

        private static readonly Color PanelColor = UiTokens.Surface0;
        private static readonly Color CellColor = UiTokens.Surface1;
        private static readonly Color StatColor = UiTokens.Surface2;
        private static readonly Color LineColor = UiTokens.Line;
        private static readonly Color CellLineColor = UiTokens.LineSoft;
        private static readonly Color InkColor = UiTokens.Ink;
        private static readonly Color InkBrightColor = UiTokens.InkBright;
        private static readonly Color BodyColor = UiTokens.Body;
        private static readonly Color MutedColor = UiTokens.Muted;
        private static readonly Color FaintColor = UiTokens.Faint;
        private static readonly Color GoldColor = UiTokens.Accent;
        private static readonly Color GoldBrightColor = UiTokens.AccentBright;
        private static readonly Color DangerColor = UiTokens.Danger;
        private static readonly Color OkColor = UiTokens.Ok;
        private static readonly Color ErrorColor = UiTokens.DangerText;

        private BagFilter _filter = BagFilter.All;
        private SortMode _sort = SortMode.Type;
        private string _search = string.Empty;
        private IItemContainer _selectedContainer;
        private InventoryItemBehaviour _selectedCell;
        private int _dropQuantity = 1;
        private bool _dropOpen;
        private bool _targetOpen;
        private MessageKind _messageKind = MessageKind.None;
        private string _messageText;

        private readonly List<InventoryItemBehaviour> _itemCells = new();

        private Control _gridContent;
        private HBoxContainer _tabsRow;
        private LineEdit _searchEdit;
        private OptionButton _sortOption;
        private HBoxContainer _apBox;
        private HBoxContainer _apPips;
        private Label _shownLabel;
        private VBoxContainer _passport;
        private Label _dropQuantityLabel;
        private System.Action<string, int> _onInventoryChange;

        // Scene nodes (inventory_panel.tscn): static layout lives in the scene,
        // the behaviour resolves them once and owns only the dynamic layer.
        private Label _title;
        private Button _closeButton;
        private Label _hintLabel;
        private Label _apLabel;

        /// <summary>Subscribes to inventory changes so the grid refreshes live while the panel is open.</summary>
        public override void _Ready()
        {
            base._Ready();
            _onInventoryChange = (_, _) => Refresh();
            GameData.Instance.Runtime.Inventory.Change += _onInventoryChange;
            MouseFilter = MouseFilterEnum.Stop;
        }

        /// <summary>Unsubscribes from inventory change events so a closed panel stops refreshing.</summary>
        public override void _ExitTree()
        {
            base._ExitTree();
            GameData.Instance.Runtime.Inventory.Change -= _onInventoryChange;
        }

        protected override void OnceConstruct()
        {
            MouseFilter = MouseFilterEnum.Stop;

            // Scene nodes (inventory_panel.tscn); static layout lives in the scene.
            _title = GetNode<Label>("Panel/Margin/Root/Header/Title");
            _tabsRow = GetNode<HBoxContainer>("Panel/Margin/Root/Header/Tabs");
            _apBox = GetNode<HBoxContainer>("Panel/Margin/Root/Header/ApBox");
            _apLabel = GetNode<Label>("Panel/Margin/Root/Header/ApBox/ApLabel");
            _apPips = GetNode<HBoxContainer>("Panel/Margin/Root/Header/ApBox/ApPips");
            _closeButton = GetNode<Button>("Panel/Margin/Root/Header/CloseButton");
            _searchEdit = GetNode<LineEdit>("Panel/Margin/Root/Toolbar/Search");
            _sortOption = GetNode<OptionButton>("Panel/Margin/Root/Toolbar/SortOption");
            _gridContent = GetNode<Control>("Panel/Margin/Root/Body/Left/GridScroll/GridContent");
            _shownLabel = GetNode<Label>("Panel/Margin/Root/Body/Left/GridFooter/ShownLabel");
            _hintLabel = GetNode<Label>("Panel/Margin/Root/Body/Left/GridFooter/HintLabel");
            _passport = GetNode<VBoxContainer>("Panel/Margin/Root/Body/Right/PassportScroll/Passport");

            ApplyTheme();
            ApplyCaptions();

            _searchEdit.TextChanged += OnSearchChanged;
            _sortOption.ItemSelected += OnSortSelected;
            _closeButton.Pressed += OnClosePressed;
        }

        /// <summary>Theme overrides for scene nodes that depend on UiKit tokens (styles stay code-side like the rest of the toolkit).</summary>
        private void ApplyTheme()
        {
            var panel = GetNode<Panel>("Panel");
            panel.AddThemeStyleboxOverride("panel", MakePanelStyle(PanelColor, LineColor, 8, 0f, 0f, true));

            _title.AddThemeColorOverride("font_color", InkBrightColor);
            _apLabel.AddThemeColorOverride("font_color", MutedColor);
            _shownLabel.AddThemeColorOverride("font_color", FaintColor);
            _hintLabel.AddThemeColorOverride("font_color", FaintColor);

            _searchEdit.PlaceholderText = InventoryItemUi.Translate("ui/inventory/search");
            _searchEdit.AddThemeFontSizeOverride("font_size", 13);
            _searchEdit.AddThemeStyleboxOverride("normal", MakeInputStyle(false));
            _searchEdit.AddThemeStyleboxOverride("focus", MakeInputStyle(true));
            _searchEdit.AddThemeColorOverride("font_color", InkColor);
            _searchEdit.AddThemeColorOverride("font_placeholder_color", FaintColor);
            _searchEdit.AddThemeColorOverride("caret_color", GoldColor);

            _sortOption.AddThemeFontSizeOverride("font_size", 13);
            _sortOption.AddThemeStyleboxOverride("normal", MakeInputStyle(false));
            _sortOption.AddThemeStyleboxOverride("hover", MakeInputStyle(true));
            _sortOption.AddThemeStyleboxOverride("pressed", MakeInputStyle(true));
            _sortOption.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            _sortOption.AddThemeColorOverride("font_color", BodyColor);
            _sortOption.AddThemeColorOverride("font_hover_color", InkBrightColor);
            var popup = _sortOption.GetPopup();
            popup.AddThemeStyleboxOverride("panel", MakePanelStyle(StatColor, LineColor, 6, 4f, 4f, true));
            popup.AddThemeStyleboxOverride("hover", MakeHoverRowStyle());
            popup.AddThemeColorOverride("font_color", BodyColor);
            popup.AddThemeColorOverride("font_hover_color", GoldBrightColor);
            popup.AddThemeConstantOverride("v_separation", 8);

            var right = GetNode<PanelContainer>("Panel/Margin/Root/Body/Right");
            right.AddThemeStyleboxOverride("panel", MakeRightColumnStyle());

            var backdrop = GetNode<ColorRect>("Backdrop");
            UiKit.ApplyPanelBackdrop(backdrop);

            CthangoverTheme.ApplyIcon(_closeButton);
        }

        /// <summary>Localized captions for all static labels of the scene.</summary>
        private void ApplyCaptions()
        {
            _title.Text = InventoryItemUi.Translate("ui/inventory/title").ToUpperInvariant();
            _apLabel.Text = InventoryItemUi.Translate("ui/inventory/ap").ToUpperInvariant();
            _hintLabel.Text = InventoryItemUi.Translate("ui/inventory/hint_select");
            _sortOption.AddItem(InventoryItemUi.Translate("ui/inventory/sort_type"), (int)SortMode.Type);
            _sortOption.AddItem(InventoryItemUi.Translate("ui/inventory/sort_name"), (int)SortMode.Name);
            _sortOption.AddItem(InventoryItemUi.Translate("ui/inventory/sort_cost"), (int)SortMode.Cost);
            _sortOption.AddItem(InventoryItemUi.Translate("ui/inventory/sort_count"), (int)SortMode.Count);
            _sortOption.Selected = 0;
        }

        protected override void ShowConstruct()
        {
            _filter = BagFilter.All;
            _sort = SortMode.Type;
            _search = string.Empty;
            _messageKind = MessageKind.None;
            _messageText = null;

            if (_searchEdit != null)
                _searchEdit.Text = string.Empty;
            if (_sortOption != null)
                _sortOption.Selected = 0;

            ClearGrid();
            ClearSelection();
            RebuildTabs();
            RebuildGrid();
            UpdateActiveActorState();
            SelectFirst();
        }

        protected override void HideDestruct()
        {
            ClearGrid();
            ClearSelection();
            ClearChildren(_passport);
        }

        /// <summary>
        /// Rebuilds the grid and passport from the current inventory state,
        /// preserving the selected item by ID when it is still present. Called
        /// automatically by <see cref="IInventory.Change"/>.
        /// </summary>
        public void Refresh()
        {
            if (!Visible || _gridContent == null)
                return;

            var previousId = _selectedContainer?.Item?.ID;
            ClearGrid();
            ClearSelection();
            RebuildGrid();

            if (previousId != null)
            {
                var container = GameData.Instance.Runtime.Inventory.TryGet(previousId);
                if (container != null)
                {
                    SelectItem(container);
                    return;
                }
            }

            SelectFirst();
        }

        private void RebuildTabs()
        {
            ClearChildren(_tabsRow);
            var inventory = GameData.Instance.Runtime.Inventory;
            var total = inventory.Items.Count;

            foreach (var tab in FilterTabs)
            {
                var count = tab.Filter == BagFilter.All ? total : CountByFilter(tab.Filter);
                var button = new Button();
                button.Text = InventoryItemUi.Translate(tab.Key) + " " + count;
                button.FocusMode = FocusModeEnum.None;
                StyleTabButton(button, _filter == tab.Filter);
                var filter = tab.Filter;
                button.Pressed += () => OnTabPressed(filter);
                _tabsRow.AddChild(button);
            }
        }

        private int CountByFilter(BagFilter filter)
        {
            var flag = FilterFlag(filter);
            var count = 0;
            foreach (var container in GameData.Instance.Runtime.Inventory.Items)
            {
                if (container?.Item != null && container.Item.ItemType.HasFlag(flag))
                    count++;
            }
            return count;
        }

        private void RebuildGrid()
        {
            ClearGrid();
            if (_gridContent == null)
                return;

            var inventory = GameData.Instance.Runtime.Inventory;
            var containers = GetFilteredContainers();
            var cellSize = CalculateCellSize();

            for (var i = 0; i < containers.Count; i++)
            {
                var container = containers[i];
                var cell = new InventoryItemBehaviour();
                cell.Construct(container);
                cell.Clicked += OnCellClicked;
                _gridContent.AddChild(cell);
                PlaceCell(cell, i, cellSize);
                _itemCells.Add(cell);
            }

            if (_itemCells.Count > 0)
            {
                var last = _itemCells[_itemCells.Count - 1];
                var height = last.Position.Y + last.Size.Y + CellGap;
                _gridContent.CustomMinimumSize = new Vector2(0, height);
            }
            else
            {
                _gridContent.CustomMinimumSize = Vector2.Zero;
            }

            if (_shownLabel != null)
            {
                _shownLabel.Text = string.Format(
                    InventoryItemUi.Translate("ui/inventory/shown"),
                    containers.Count,
                    inventory.Items.Count);
            }
        }

        private List<IItemContainer> GetFilteredContainers()
        {
            var result = new List<IItemContainer>();
            foreach (var container in GameData.Instance.Runtime.Inventory.Items)
            {
                if (container?.Item == null)
                    continue;
                if (!MatchesFilter(container.Item) || !MatchesSearch(container.Item))
                    continue;
                result.Add(container);
            }
            SortContainers(result);
            return result;
        }

        private bool MatchesFilter(IItem item)
        {
            return _filter == BagFilter.All || item.ItemType.HasFlag(FilterFlag(_filter));
        }

        private bool MatchesSearch(IItem item)
        {
            if (string.IsNullOrWhiteSpace(_search))
                return true;

            var needle = _search.Trim().ToLowerInvariant();
            if (InventoryItemUi.Translate(item.Name).ToLowerInvariant().Contains(needle))
                return true;
            return item.ID != null && item.ID.ToLowerInvariant().Contains(needle);
        }

        private void SortContainers(List<IItemContainer> containers)
        {
            switch (_sort)
            {
                case SortMode.Name:
                    containers.Sort((a, b) => string.Compare(TranslateName(a), TranslateName(b), StringComparison.OrdinalIgnoreCase));
                    break;
                case SortMode.Cost:
                    containers.Sort((a, b) => b.Item.Cost.CompareTo(a.Item.Cost));
                    break;
                case SortMode.Count:
                    containers.Sort((a, b) => b.Count.CompareTo(a.Count));
                    break;
                default:
                    containers.Sort(CompareByType);
                    break;
            }
        }

        private static int CompareByType(IItemContainer a, IItemContainer b)
        {
            var rankA = TypeRank(a.Item.ItemType);
            var rankB = TypeRank(b.Item.ItemType);
            if (rankA != rankB)
                return rankA - rankB;
            return string.Compare(TranslateName(a), TranslateName(b), StringComparison.OrdinalIgnoreCase);
        }

        private static int TypeRank(ItemType type)
        {
            if (type.HasFlag(ItemType.Quest))
                return 0;
            if (type.HasFlag(ItemType.Recipe))
                return 1;
            if (type.HasFlag(ItemType.Food))
                return 2;
            return 3;
        }

        private static string TranslateName(IItemContainer container)
        {
            return InventoryItemUi.Translate(container.Item.Name);
        }

        private static ItemType FilterFlag(BagFilter filter)
        {
            switch (filter)
            {
                case BagFilter.Food:
                    return ItemType.Food;
                case BagFilter.Resource:
                    return ItemType.Resource;
                case BagFilter.Recipe:
                    return ItemType.Recipe;
                case BagFilter.Quest:
                    return ItemType.Quest;
                default:
                    return ItemType.None;
            }
        }

        private float CalculateCellSize()
        {
            var viewportWidth = GetViewportRect().Size.X;
            if (viewportWidth <= 0)
                viewportWidth = 1920f;

            var panelWidth = viewportWidth - 80f - 48f;
            var leftWidth = Mathf.Max(320f, panelWidth - PassportWidth - 14f);
            var cellSize = (leftWidth - (Columns - 1) * CellGap) / Columns;
            return Mathf.Clamp(cellSize, 72f, 220f);
        }

        private void PlaceCell(InventoryItemBehaviour cell, int index, float cellSize)
        {
            var column = index % Columns;
            var row = index / Columns;
            cell.Position = new Vector2(column * (cellSize + CellGap), row * (cellSize + CellGap));
            cell.Size = new Vector2(cellSize, cellSize);
        }

        private void SelectItem(IItemContainer container)
        {
            if (_selectedCell != null)
            {
                _selectedCell.SetSelected(false);
                _selectedCell = null;
            }

            _selectedContainer = container;
            _dropOpen = false;
            _dropQuantity = 1;
            _targetOpen = false;

            if (container != null)
            {
                foreach (var cell in _itemCells)
                {
                    if (ReferenceEquals(cell.Model, container))
                    {
                        _selectedCell = cell;
                        cell.SetSelected(true);
                        break;
                    }
                }
            }

            BuildPassport();
        }

        private void SelectFirst()
        {
            if (_itemCells.Count > 0)
                SelectItem(_itemCells[0].Model);
            else
                SelectItem(null);
        }

        private void ClearGrid()
        {
            foreach (var cell in _itemCells)
                cell.QueueFree();
            _itemCells.Clear();
            _selectedCell = null;
        }

        private void ClearSelection()
        {
            if (_selectedCell != null)
            {
                _selectedCell.SetSelected(false);
                _selectedCell = null;
            }
            _selectedContainer = null;
            _dropOpen = false;
            _dropQuantity = 1;
            _targetOpen = false;
        }

        private void BuildPassport()
        {
            ClearChildren(_passport);
            var container = _selectedContainer;
            if (container?.Item == null)
            {
                var hasAny = GameData.Instance.Runtime.Inventory.Items.Count > 0;
                var placeholder = MakeLabel(
                    InventoryItemUi.Translate(hasAny ? "ui/inventory/select" : "ui/inventory/empty"),
                    14, FaintColor);
                placeholder.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                placeholder.HorizontalAlignment = HorizontalAlignment.Center;
                _passport.AddChild(placeholder);
                return;
            }

            var item = container.Item;
            _passport.AddChild(BuildPassportHeader(container));

            var description = MakeLabel(InventoryItemUi.Translate(item.Description), 14, BodyColor);
            description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _passport.AddChild(description);

            _passport.AddChild(BuildStatsBlock(container));
            _passport.AddChild(BuildPropertiesBlock(item));
            _passport.AddChild(BuildActionsBlock(container));
        }

        private Control BuildPassportHeader(IItemContainer container)
        {
            var item = container.Item;
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 16);

            var iconPanel = new PanelContainer();
            iconPanel.CustomMinimumSize = new Vector2(96, 96);
            iconPanel.AddThemeStyleboxOverride("panel", MakePanelStyle(CellColor, CellLineColor, 8));
            var icon = new TextureRect();
            icon.Texture = item.Sprite;
            icon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            icon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
            icon.CustomMinimumSize = new Vector2(80, 80);
            icon.MouseFilter = MouseFilterEnum.Ignore;
            iconPanel.AddChild(icon);
            row.AddChild(iconPanel);

            var titleColumn = new VBoxContainer();
            titleColumn.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            titleColumn.AddThemeConstantOverride("separation", 8);

            var name = MakeLabel(InventoryItemUi.Translate(item.Name), 25, InkBrightColor);
            name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            titleColumn.AddChild(name);

            var badges = new HFlowContainer();
            badges.AddThemeConstantOverride("h_separation", 5);
            badges.AddThemeConstantOverride("v_separation", 5);
            foreach (var type in InventoryItemUi.BadgeOrder)
            {
                if (item.ItemType.HasFlag(type))
                    badges.AddChild(MakeBadge(type));
            }
            titleColumn.AddChild(badges);
            row.AddChild(titleColumn);

            var costColumn = new VBoxContainer();
            costColumn.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            var cost = MakeLabel(item.Cost.ToString(), 22, GoldBrightColor);
            cost.HorizontalAlignment = HorizontalAlignment.Right;
            var costLabel = MakeLabel(InventoryItemUi.Translate("ui/inventory/cost").ToUpperInvariant(), 10, FaintColor);
            costLabel.HorizontalAlignment = HorizontalAlignment.Right;
            costColumn.AddChild(cost);
            costColumn.AddChild(costLabel);
            row.AddChild(costColumn);

            return row;
        }

        private static Control MakeBadge(ItemType type)
        {
            return UiKit.Badge(
                InventoryItemUi.Translate(InventoryItemUi.TypeBadgeKey(type)),
                InventoryItemUi.AccentColor(type));
        }

        private static Control BuildStatsBlock(IItemContainer container)
        {
            var item = container.Item;
            var wrapper = new PanelContainer();
            wrapper.AddThemeStyleboxOverride("panel", MakePanelStyle(LineColor, LineColor, 6));

            var grid = new GridContainer();
            grid.Columns = 2;
            grid.AddThemeConstantOverride("h_separation", 1);
            grid.AddThemeConstantOverride("v_separation", 1);
            grid.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            wrapper.AddChild(grid);

            grid.AddChild(MakeStatCell(InventoryItemUi.Translate("ui/inventory/stack"), "×" + container.Count));
            grid.AddChild(MakeStatCell(InventoryItemUi.Translate("ui/inventory/id"), item.ID));
            grid.AddChild(MakeStatCell(InventoryItemUi.Translate("ui/inventory/action"), item.ItemAction?.ID ?? "—"));
            grid.AddChild(MakeStatCell(InventoryItemUi.Translate("ui/inventory/verb"), InventoryItemUi.Translate(InventoryItemUi.VerbKey(item))));
            return wrapper;
        }

        private static Control MakeStatCell(string key, string value)
        {
            var cell = new PanelContainer();
            cell.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            cell.AddThemeStyleboxOverride("panel", MakePanelStyle(StatColor, StatColor, 0, 12f, 8f));

            var column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", 3);
            column.AddChild(MakeLabel(key.ToUpperInvariant(), 10, FaintColor));

            var valueLabel = MakeLabel(value, 13, InkColor);
            valueLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            column.AddChild(valueLabel);
            cell.AddChild(column);
            return cell;
        }

        private static Control BuildPropertiesBlock(IItem item)
        {
            var block = new VBoxContainer();
            block.AddThemeConstantOverride("separation", 7);
            block.AddChild(MakeLabel(InventoryItemUi.Translate("ui/inventory/props_header").ToUpperInvariant(), 10, FaintColor));

            var values = item.Properties?.Values;
            if (values == null || values.Count == 0)
            {
                block.AddChild(MakePropertyRow(InventoryItemUi.Translate("ui/inventory/props_none"), string.Empty));
                return block;
            }

            var keys = new List<string>(values.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var key in keys)
                block.AddChild(MakePropertyRow(key, values[key]));
            return block;
        }

        private static Control MakePropertyRow(string key, string value)
        {
            var row = new PanelContainer();
            row.AddThemeStyleboxOverride("panel", MakePanelStyle(StatColor, StatColor, 0, 12f, 7f));

            var box = new HBoxContainer();
            box.AddThemeConstantOverride("separation", 12);

            var keyLabel = MakeLabel(key, 12, MutedColor);
            keyLabel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            box.AddChild(keyLabel);

            var valueLabel = MakeLabel(value, 12, GoldBrightColor);
            box.AddChild(valueLabel);
            row.AddChild(box);
            return row;
        }

        private Control BuildActionsBlock(IItemContainer container)
        {
            var item = container.Item;
            var block = new VBoxContainer();
            block.AddThemeConstantOverride("separation", 9);

            var usable = InventoryItemUi.IsUsable(item);
            var verb = InventoryItemUi.Translate(InventoryItemUi.VerbKey(item));

            var useButton = new Button();
            useButton.Text = HasActiveActor
                ? verb + "  " + InventoryItemUi.Translate("ui/inventory/ap_cost")
                : verb;
            useButton.Disabled = !usable;
            useButton.FocusMode = FocusModeEnum.None;
            StyleButton(useButton, true, false);
            useButton.Pressed += OnUseClicked;
            block.AddChild(useButton);

            if (!usable)
            {
                var hint = MakeLabel(InventoryItemUi.Translate("ui/inventory/no_action"), 12, FaintColor);
                hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                block.AddChild(hint);
            }

            var canDrop = InventoryItemUi.CanDrop(item);
            var dropButton = new Button();
            dropButton.Text = InventoryItemUi.Translate(canDrop ? "ui/inventory/drop" : "ui/inventory/drop_cant");
            dropButton.Disabled = !canDrop;
            dropButton.FocusMode = FocusModeEnum.None;
            StyleButton(dropButton, false, true);
            dropButton.Pressed += OnDropClicked;
            block.AddChild(dropButton);

            if (_dropOpen)
                block.AddChild(BuildDropQuantity(container));

            if (_targetOpen && item.ItemType.HasFlag(ItemType.TargetUsed))
                block.AddChild(BuildTargetPanel());

            if (_messageKind != MessageKind.None)
                block.AddChild(BuildMessage());

            return block;
        }

        private Control BuildDropQuantity(IItemContainer container)
        {
            var wrapper = new PanelContainer();
            wrapper.AddThemeStyleboxOverride("panel", MakePanelStyle(StatColor, LineColor, 6, 12f, 12f));

            var column = new VBoxContainer();
            column.AddThemeConstantOverride("separation", 8);

            var head = new HBoxContainer();
            var title = MakeLabel(InventoryItemUi.Translate("ui/inventory/drop_quantity"), 12, MutedColor);
            title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            head.AddChild(title);
            _dropQuantityLabel = MakeLabel(_dropQuantity + " / " + container.Count, 12, GoldBrightColor);
            head.AddChild(_dropQuantityLabel);
            column.AddChild(head);

            var slider = new HSlider();
            slider.MinValue = 1;
            slider.MaxValue = Mathf.Max(1, container.Count);
            slider.Step = 1;
            slider.Value = Mathf.Clamp(_dropQuantity, 1, Mathf.Max(1, container.Count));
            slider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            slider.AddThemeStyleboxOverride("slider", MakeSliderTrack(UiTokens.LineDisabled));
            slider.AddThemeStyleboxOverride("grabber_area", MakeSliderTrack(GoldColor));
            slider.AddThemeStyleboxOverride("grabber_area_highlight", MakeSliderTrack(GoldBrightColor));
            slider.ValueChanged += OnDropSliderChanged;
            column.AddChild(slider);

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            row.AddChild(MakeSmallButton("−", OnDropMinus));
            row.AddChild(MakeSmallButton("+", OnDropPlus));
            row.AddChild(MakeSmallButton(InventoryItemUi.Translate("ui/inventory/drop_all"), OnDropMax));

            var confirm = new Button();
            confirm.Text = InventoryItemUi.Translate("ui/inventory/drop_confirm");
            confirm.FocusMode = FocusModeEnum.None;
            confirm.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            StyleButton(confirm, false, true);
            confirm.Pressed += OnDropConfirm;
            row.AddChild(confirm);
            column.AddChild(row);

            wrapper.AddChild(column);
            return wrapper;
        }

        private Control BuildTargetPanel()
        {
            var targetPanel = new CharacterTargetPanel();
            targetPanel.CharacterSelected += OnTargetSelected;
            targetPanel.Cancelled += OnTargetCancelled;
            return targetPanel;
        }

        private Control BuildMessage()
        {
            var accent = _messageKind == MessageKind.Ok ? OkColor : ErrorColor;
            var wrapper = new PanelContainer();
            wrapper.AddThemeStyleboxOverride("panel", MakePanelStyle(
                UiTokens.Alpha(accent, 0.1f),
                UiTokens.Alpha(accent, 0.5f), 5, 12f, 9f));

            var label = MakeLabel(_messageText, 12, accent);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            wrapper.AddChild(label);
            return wrapper;
        }

        private Button MakeSmallButton(string text, Action handler)
        {
            var button = new Button();
            button.Text = text;
            button.FocusMode = FocusModeEnum.None;
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            StyleButton(button, false, false);
            button.AddThemeFontSizeOverride("font_size", 13);
            button.Pressed += handler;
            return button;
        }

        private bool HasActiveActor => ActiveCharacterRegistry.ActiveCharacter != null;

        private void UpdateActiveActorState()
        {
            if (_apBox == null)
                return;

            _apBox.Visible = HasActiveActor;
            if (!HasActiveActor || _apPips == null)
                return;

            ClearChildren(_apPips);
            var point = ActiveCharacterRegistry.ActiveCharacter.Attributes.Point;
            var total = point.BaseValue > 0 ? point.BaseValue : point.Value;
            total = Mathf.Clamp(total, 1, 6);
            for (var i = 0; i < total; i++)
            {
                var pip = new Panel();
                pip.CustomMinimumSize = new Vector2(11, 11);
                var style = new StyleBoxFlat();
                style.CornerRadiusTopLeft = 6;
                style.CornerRadiusTopRight = 6;
                style.CornerRadiusBottomLeft = 6;
                style.CornerRadiusBottomRight = 6;
                if (i < point.Value)
                {
                    style.BgColor = GoldBrightColor;
                    style.BorderColor = GoldColor;
                }
                else
                {
                    style.BgColor = UiTokens.LineDisabled;
                    style.BorderColor = UiTokens.LineFaint;
                }
                style.BorderWidthLeft = 1;
                style.BorderWidthRight = 1;
                style.BorderWidthTop = 1;
                style.BorderWidthBottom = 1;
                pip.AddThemeStyleboxOverride("panel", style);
                pip.MouseFilter = MouseFilterEnum.Ignore;
                _apPips.AddChild(pip);
            }
        }

        private bool HasActionPoint()
        {
            if (!HasActiveActor)
                return true;

            var point = ActiveCharacterRegistry.ActiveCharacter.Attributes.Point;
            if (point.Value >= 1)
                return true;

            SetMessage(MessageKind.Error, InventoryItemUi.Translate("ui/inventory/no_ap"));
            BuildPassport();
            PlaySound("ui/close_click");
            return false;
        }

        private void SpendActionPoint()
        {
            if (!HasActiveActor)
                return;
            ActiveCharacterRegistry.ActiveCharacter.Attributes.Point.Value -= 1;
            UpdateActiveActorState();
        }

        private void OnUseClicked()
        {
            var container = _selectedContainer;
            var item = container?.Item;
            if (item?.ItemAction == null || !InventoryItemUi.IsUsable(item))
                return;

            if (item.ItemType.HasFlag(ItemType.TargetUsed))
            {
                _targetOpen = true;
                _dropOpen = false;
                BuildPassport();
                return;
            }

            if (!HasActionPoint())
                return;

            if (!item.ItemAction.UseAction(item))
            {
                SetMessage(MessageKind.Error, InventoryItemUi.Translate("ui/inventory/use_failed"));
                BuildPassport();
                return;
            }

            SpendActionPoint();
            var messageKey = item.ItemType.HasFlag(ItemType.Recipe)
                ? "ui/inventory/used_recipe"
                : "ui/inventory/used";
            SetMessage(MessageKind.Ok, string.Format(
                InventoryItemUi.Translate(messageKey),
                InventoryItemUi.Translate(item.Name)));
            GameData.Instance.Runtime.Inventory.Remove(item, 1);
            PlaySound("items/use");
        }

        private void OnDropClicked()
        {
            if (_selectedContainer?.Item == null)
                return;

            _dropQuantity = 1;
            _dropOpen = !_dropOpen;
            _targetOpen = false;
            BuildPassport();
        }

        private void OnDropSliderChanged(double value)
        {
            _dropQuantity = Mathf.RoundToInt((float)value);
            if (_dropQuantityLabel != null && _selectedContainer != null)
                _dropQuantityLabel.Text = _dropQuantity + " / " + _selectedContainer.Count;
        }

        private void OnDropMinus()
        {
            if (_selectedContainer == null)
                return;
            _dropQuantity = Mathf.Max(_dropQuantity - 1, 1);
            RefreshDropQuantity();
        }

        private void OnDropPlus()
        {
            if (_selectedContainer == null)
                return;
            _dropQuantity = Mathf.Min(_dropQuantity + 1, _selectedContainer.Count);
            RefreshDropQuantity();
        }

        private void OnDropMax()
        {
            if (_selectedContainer == null)
                return;
            _dropQuantity = _selectedContainer.Count;
            RefreshDropQuantity();
        }

        private void RefreshDropQuantity()
        {
            BuildPassport();
        }

        private void OnDropConfirm()
        {
            var container = _selectedContainer;
            var item = container?.Item;
            if (item == null || !InventoryItemUi.CanDrop(item))
                return;
            if (!HasActionPoint())
                return;

            SpendActionPoint();
            var quantity = Mathf.Clamp(_dropQuantity, 1, container.Count);
            SetMessage(MessageKind.Ok, string.Format(
                InventoryItemUi.Translate("ui/inventory/dropped"),
                InventoryItemUi.Translate(item.Name),
                quantity));
            GameData.Instance.Runtime.Inventory.Remove(item, quantity);
            PlaySound("ui/close_click");
        }

        private void OnTargetSelected(CharacterInfoData info)
        {
            var item = _selectedContainer?.Item;
            if (item?.ItemAction == null)
                return;

            if (!HasActionPoint())
                return;

            var applied = item.ItemAction is ITargetedItemAction targeted
                ? targeted.UseOn(item, info)
                : item.ItemAction.UseAction(item);
            if (!applied)
            {
                SetMessage(MessageKind.Error, InventoryItemUi.Translate("ui/inventory/applied_failed"));
                _targetOpen = false;
                BuildPassport();
                return;
            }

            SpendActionPoint();
            var template = info != null ? CharacterTemplateRegistry.Get(info.ID) : null;
            var targetName = InventoryItemUi.Translate(template?.Name ?? info?.ID ?? "?");
            SetMessage(MessageKind.Ok, string.Format(
                InventoryItemUi.Translate("ui/inventory/applied"),
                InventoryItemUi.Translate(item.Name),
                targetName));
            GameData.Instance.Runtime.Inventory.Remove(item, 1);
            PlaySound("items/target");
        }

        private void OnTargetCancelled()
        {
            _targetOpen = false;
            BuildPassport();
        }

        private void OnCellClicked(InventoryItemBehaviour cell)
        {
            _messageKind = MessageKind.None;
            _messageText = null;
            SelectItem(cell.Model);
            PlaySound("ui/skip");
        }

        private void OnTabPressed(BagFilter filter)
        {
            _filter = filter;
            _messageKind = MessageKind.None;
            _messageText = null;
            ClearSelection();
            RebuildTabs();
            RebuildGrid();
            SelectFirst();
        }

        private void OnSearchChanged(string text)
        {
            _search = text;
            ClearSelection();
            RebuildGrid();
            SelectFirst();
        }

        private void OnSortSelected(long index)
        {
            _sort = (SortMode)index;
            ClearSelection();
            RebuildGrid();
            SelectFirst();
        }

        private void OnClosePressed()
        {
            PlaySound("ui/close_click");
            Hide();
        }

        private void SetMessage(MessageKind kind, string text)
        {
            _messageKind = kind;
            _messageText = text;
        }

        private void PlaySound(string id)
        {
            var audio = SceneContextNode.FindNode<Node>("AudioService") as IAudioService;
            audio?.PlaySound(id, SoundType.UI);
        }

        private static Label MakeLabel(string text, int size, Color color)
        {
            var label = new Label();
            label.Text = text ?? string.Empty;
            label.AddThemeFontSizeOverride("font_size", size);
            label.AddThemeColorOverride("font_color", color);
            label.MouseFilter = MouseFilterEnum.Ignore;
            return label;
        }

        private static StyleBoxFlat MakePanelStyle(Color background, Color border, int radius,
            float paddingX = 0f, float paddingY = 0f, bool shadow = false)
        {
            var style = new StyleBoxFlat();
            style.BgColor = background;
            style.BorderColor = border;
            style.BorderWidthLeft = 1;
            style.BorderWidthRight = 1;
            style.BorderWidthTop = 1;
            style.BorderWidthBottom = 1;
            style.CornerRadiusTopLeft = radius;
            style.CornerRadiusTopRight = radius;
            style.CornerRadiusBottomLeft = radius;
            style.CornerRadiusBottomRight = radius;
            style.ContentMarginLeft = paddingX;
            style.ContentMarginRight = paddingX;
            style.ContentMarginTop = paddingY;
            style.ContentMarginBottom = paddingY;
            if (shadow)
            {
                style.ShadowColor = new Color(0f, 0f, 0f, 0.6f);
                style.ShadowSize = 18;
                style.ShadowOffset = new Vector2(0f, 8f);
            }
            return style;
        }

        private static StyleBoxFlat MakeRightColumnStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = UiTokens.SideBg;
            style.BorderColor = LineColor;
            style.BorderWidthLeft = 1;
            style.ContentMarginLeft = 20;
            style.ContentMarginRight = 18;
            style.ContentMarginTop = 16;
            style.ContentMarginBottom = 16;
            return style;
        }

        private static StyleBoxFlat MakeInputStyle(bool focus)
        {
            var style = new StyleBoxFlat();
            style.BgColor = UiTokens.Well;
            style.BorderColor = focus ? UiTokens.AccentRing : LineColor;
            style.BorderWidthLeft = 1;
            style.BorderWidthRight = 1;
            style.BorderWidthTop = 1;
            style.BorderWidthBottom = 1;
            style.CornerRadiusTopLeft = 4;
            style.CornerRadiusTopRight = 4;
            style.CornerRadiusBottomLeft = 4;
            style.CornerRadiusBottomRight = 4;
            style.ContentMarginLeft = 10;
            style.ContentMarginRight = 10;
            style.ContentMarginTop = 8;
            style.ContentMarginBottom = 8;
            return style;
        }

        private static StyleBoxFlat MakeHoverRowStyle()
        {
            var style = new StyleBoxFlat();
            style.BgColor = UiTokens.AccentTrace;
            style.CornerRadiusTopLeft = 4;
            style.CornerRadiusTopRight = 4;
            style.CornerRadiusBottomLeft = 4;
            style.CornerRadiusBottomRight = 4;
            style.ContentMarginLeft = 8;
            style.ContentMarginRight = 8;
            style.ContentMarginTop = 4;
            style.ContentMarginBottom = 4;
            return style;
        }

        private static StyleBoxFlat MakeSliderTrack(Color color)
        {
            var style = new StyleBoxFlat();
            style.BgColor = color;
            style.CornerRadiusTopLeft = 3;
            style.CornerRadiusTopRight = 3;
            style.CornerRadiusBottomLeft = 3;
            style.CornerRadiusBottomRight = 3;
            style.ContentMarginTop = 2;
            style.ContentMarginBottom = 2;
            return style;
        }

        private static StyleBoxFlat MakeButtonStyle(bool primary, bool danger, bool hover)
        {
            var style = new StyleBoxFlat();
            if (primary)
                style.BgColor = hover ? UiTokens.BtnPrimaryHover : UiTokens.BtnPrimary;
            else
                style.BgColor = hover ? UiTokens.BtnHover : UiTokens.Surface3;

            if (danger && hover)
                style.BorderColor = DangerColor;
            else if (primary)
                style.BorderColor = hover ? UiTokens.AccentLineStrong : UiTokens.Alpha(UiTokens.Accent, 0.55f);
            else
                style.BorderColor = hover ? UiTokens.Alpha(UiTokens.Accent, 0.55f) : LineColor;

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
            style.ContentMarginTop = 11;
            style.ContentMarginBottom = 11;
            return style;
        }

        private static void StyleButton(Button button, bool primary, bool danger)
        {
            button.AddThemeFontSizeOverride("font_size", 14);
            button.AddThemeStyleboxOverride("normal", MakeButtonStyle(primary, danger, false));
            button.AddThemeStyleboxOverride("hover", MakeButtonStyle(primary, danger, true));
            button.AddThemeStyleboxOverride("pressed", MakeButtonStyle(primary, danger, true));

            var disabled = MakeButtonStyle(primary, danger, false);
            disabled.BgColor = UiTokens.BtnDisabled;
            disabled.BorderColor = UiTokens.LineDisabled;
            button.AddThemeStyleboxOverride("disabled", disabled);
            button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());

            var normalColor = primary ? GoldBrightColor : BodyColor;
            var hoverColor = danger ? ErrorColor : (primary ? UiTokens.AccentHover : InkBrightColor);
            button.AddThemeColorOverride("font_color", normalColor);
            button.AddThemeColorOverride("font_hover_color", hoverColor);
            button.AddThemeColorOverride("font_pressed_color", hoverColor);
            button.AddThemeColorOverride("font_disabled_color", FaintColor);
        }

        private static void StyleTabButton(Button button, bool active)
        {
            button.AddThemeFontSizeOverride("font_size", 13);
            button.AddThemeStyleboxOverride("normal", MakeTabStyle(active, false));
            button.AddThemeStyleboxOverride("hover", MakeTabStyle(active, true));
            button.AddThemeStyleboxOverride("pressed", MakeTabStyle(active, true));
            button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            button.AddThemeColorOverride("font_color", active ? GoldBrightColor : MutedColor);
            button.AddThemeColorOverride("font_hover_color", active ? GoldBrightColor : InkColor);
            button.AddThemeColorOverride("font_pressed_color", active ? GoldBrightColor : InkColor);
        }

        private static StyleBoxFlat MakeTabStyle(bool active, bool hover)
        {
            var style = new StyleBoxFlat();
            if (active)
                style.BgColor = UiTokens.AccentSoft;
            else
                style.BgColor = hover ? UiTokens.WhiteTrace : Colors.Transparent;
            style.BorderColor = active ? UiTokens.AccentLine : Colors.Transparent;
            style.BorderWidthLeft = 1;
            style.BorderWidthRight = 1;
            style.BorderWidthTop = 1;
            style.BorderWidthBottom = 1;
            style.CornerRadiusTopLeft = 4;
            style.CornerRadiusTopRight = 4;
            style.CornerRadiusBottomLeft = 4;
            style.CornerRadiusBottomRight = 4;
            style.ContentMarginLeft = 13;
            style.ContentMarginRight = 13;
            style.ContentMarginTop = 7;
            style.ContentMarginBottom = 7;
            return style;
        }

        private static void ClearChildren(Node container)
        {
            if (container == null)
                return;
            foreach (var child in container.GetChildren())
            {
                container.RemoveChild(child);
                child.QueueFree();
            }
        }
    }
}
