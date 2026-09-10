using System.Collections.Generic;
using Cthangover.Core.Audio;
using Cthangover.Core.Battle;
using Cthangover.Core.Items;
using Cthangover.Core.Scenes;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.UI.Inventory
{
    /// <summary>
    /// Full-screen inventory panel with a left-side item grid and a
    /// right-side detail panel. Extends <see cref="Widget"/> so its
    /// lifecycle is managed by the ToolBox toolbar — tapping the bag
    /// button toggles Show/Hide via <see cref="Switch"/>.
    ///
    /// The left split is a 6-column scrollable grid of
    /// <see cref="InventoryItemBehaviour"/> cells. Hovering highlights a
    /// cell, clicking selects it and populates the right panel with the
    /// item's translated name, description, count, and contextual action
    /// buttons. When the inventory mutates
    /// (<see cref="IInventory.Change"/>), the grid rebuilds and the
    /// selection survives if the same item ID is still present.
    ///
    /// Action costs are context-dependent: outside battle actions are
    /// free; during an active battle
    /// (<see cref="BattleSceneContext.Instance"/>), every item action
    /// costs 1 action point from the active character
    /// (<see cref="BattleSceneContext.ActiveCharacter"/>). If the
    /// character has insufficient AP, an error message is displayed and
    /// the action is blocked.
    ///
    /// For <see cref="ItemType.TargetUsed"/> items, clicking "Use"
    /// opens a <see cref="CharacterTargetPanel"/> sub-panel so the
    /// player can choose which party member receives the item effect.
    /// </summary>
    public partial class InventoryPanelBehaviour : Widget
    {
        private VBoxContainer _rightPanel;
        private Label _itemName;
        private RichTextLabel _itemDescription;
        private Label _itemCount;
        private Button _useButton;
        private Button _dropButton;
        private Label _messageLabel;
        private Label _quantityLabel;
        private HSlider _quantitySlider;
        private Button _plusButton;
        private Button _minusButton;
        private Button _maxButton;
        private Button _dropConfirmButton;
        private HBoxContainer _quantityRow;
        private VBoxContainer _quantityContainer;
        private ScrollContainer _scrollContainer;
        private Control _gridContent;

        private IItemContainer _selectedContainer;
        private InventoryItemBehaviour _selectedCell;
        private int _dropQuantity = 1;
        private readonly List<InventoryItemBehaviour> _itemCells = new();
        private CharacterTargetPanel _targetPanel;
        private PackedScene _itemScene;
        private System.Action<string, int> _onInventoryChange;

        private const int Columns = 6;
        private const float CellPadding = 4f;
        private static readonly Color HoverColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color NormalColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        private static readonly Color SelectedColor = new Color(1f, 1f, 0.6f, 1f);
        private static readonly Color ErrorColor = new Color(0.9f, 0.3f, 0.3f, 1f);

        /// <summary>
        /// Subscribes to inventory change events for auto-refresh,
        /// wires the close button from the scene tree, and ensures mouse
        /// events are captured.
        /// </summary>
        public override void _Ready()
        {
            base._Ready();
            _onInventoryChange = (_, _) => Refresh();
            GameData.Instance.Runtime.Inventory.Change += _onInventoryChange;
            MouseFilter = MouseFilterEnum.Stop;
        }

        /// <summary>
        /// Unsubscribes from the inventory change event to prevent
        /// leaks when leaving the scene tree.
        /// </summary>
        public override void _ExitTree()
        {
            base._ExitTree();
            GameData.Instance.Runtime.Inventory.Change -= _onInventoryChange;
        }

        /// <summary>
        /// Builds all UI elements programmatically: background overlay,
        /// title, left/right split with scrollable grid and detail panel,
        /// quantity selector for dropping, and close button. Called
        /// exactly once on the first Show().
        /// </summary>
        protected override void OnceConstruct()
        {
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            MouseFilter = MouseFilterEnum.Stop;

            _itemScene = GD.Load<PackedScene>("res://scenes/ui/inventory_item.tscn");

            var bg = new ColorRect();
            bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            bg.Color = new Color(0, 0, 0, 0.9f);
            bg.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(bg);

            var title = new Label();
            title.Text = TranslationServer.Translate("ui/inventory/title");
            title.AddThemeFontSizeOverride("font_size", 22);
            title.HorizontalAlignment = HorizontalAlignment.Center;
            title.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
            title.OffsetTop = 10;
            title.OffsetBottom = 40;
            title.OffsetLeft = 20;
            title.OffsetRight = -20;
            title.MouseFilter = MouseFilterEnum.Ignore;
            AddChild(title);

            var hbox = new HBoxContainer();
            hbox.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            hbox.OffsetTop = 50;
            hbox.OffsetBottom = -60;
            hbox.OffsetLeft = 20;
            hbox.OffsetRight = -20;
            hbox.AddThemeConstantOverride("separation", 10);
            AddChild(hbox);

            var leftPanel = new VBoxContainer();
            leftPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            leftPanel.SizeFlagsStretchRatio = 3;
            hbox.AddChild(leftPanel);

            _scrollContainer = new ScrollContainer();
            _scrollContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
            _scrollContainer.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
            leftPanel.AddChild(_scrollContainer);

            _gridContent = new Control();
            _gridContent.MouseFilter = MouseFilterEnum.Pass;
            _scrollContainer.AddChild(_gridContent);

            _rightPanel = new VBoxContainer();
            _rightPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _rightPanel.SizeFlagsStretchRatio = 2;
            _rightPanel.CustomMinimumSize = new Vector2(250, 0);
            _rightPanel.AddThemeConstantOverride("separation", 8);
            hbox.AddChild(_rightPanel);

            _itemName = new Label();
            _itemName.AddThemeFontSizeOverride("font_size", 18);
            _itemName.HorizontalAlignment = HorizontalAlignment.Center;
            _itemName.Visible = false;
            _rightPanel.AddChild(_itemName);

            _itemDescription = new RichTextLabel();
            _itemDescription.BbcodeEnabled = true;
            _itemDescription.ScrollActive = true;
            _itemDescription.CustomMinimumSize = new Vector2(0, 100);
            _itemDescription.SizeFlagsVertical = SizeFlags.ExpandFill;
            _itemDescription.Visible = false;
            _rightPanel.AddChild(_itemDescription);

            _itemCount = new Label();
            _itemCount.AddThemeFontSizeOverride("font_size", 16);
            _itemCount.HorizontalAlignment = HorizontalAlignment.Center;
            _itemCount.Visible = false;
            _rightPanel.AddChild(_itemCount);

            _useButton = new Button();
            _useButton.Text = TranslationServer.Translate("ui/inventory/use");
            _useButton.Visible = false;
            _useButton.Pressed += OnUseClicked;
            _rightPanel.AddChild(_useButton);

            _dropButton = new Button();
            _dropButton.Text = TranslationServer.Translate("ui/inventory/drop");
            _dropButton.Visible = false;
            _dropButton.Pressed += OnDropClicked;
            _rightPanel.AddChild(_dropButton);

            _quantityContainer = new VBoxContainer();
            _quantityContainer.Visible = false;
            _quantityContainer.AddThemeConstantOverride("separation", 4);
            _rightPanel.AddChild(_quantityContainer);

            _quantityLabel = new Label();
            _quantityLabel.AddThemeFontSizeOverride("font_size", 14);
            _quantityLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _quantityContainer.AddChild(_quantityLabel);

            _quantitySlider = new HSlider();
            _quantitySlider.MinValue = 1;
            _quantitySlider.Step = 1;
            _quantitySlider.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            _quantitySlider.ValueChanged += OnQuantitySliderChanged;
            _quantityContainer.AddChild(_quantitySlider);

            _quantityRow = new HBoxContainer();
            _quantityRow.AddThemeConstantOverride("separation", 4);
            _quantityContainer.AddChild(_quantityRow);

            _minusButton = new Button();
            _minusButton.Text = "-";
            _minusButton.Pressed += OnMinusClicked;
            _quantityRow.AddChild(_minusButton);

            _plusButton = new Button();
            _plusButton.Text = "+";
            _plusButton.Pressed += OnPlusClicked;
            _quantityRow.AddChild(_plusButton);

            _maxButton = new Button();
            _maxButton.Text = TranslationServer.Translate("ui/inventory/drop_all");
            _maxButton.Pressed += OnMaxClicked;
            _quantityRow.AddChild(_maxButton);

            _dropConfirmButton = new Button();
            _dropConfirmButton.Text = TranslationServer.Translate("ui/inventory/drop_confirm");
            _dropConfirmButton.Pressed += OnDropConfirm;
            _quantityContainer.AddChild(_dropConfirmButton);

            _messageLabel = new Label();
            _messageLabel.AddThemeFontSizeOverride("font_size", 14);
            _messageLabel.HorizontalAlignment = HorizontalAlignment.Center;
            _messageLabel.Modulate = ErrorColor;
            _messageLabel.Visible = false;
            _rightPanel.AddChild(_messageLabel);

            var closePanel = new Panel();
            closePanel.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);
            closePanel.AnchorLeft = 0.85f;
            closePanel.AnchorTop = 0.94f;
            closePanel.OffsetLeft = 0;
            closePanel.OffsetTop = 0;
            closePanel.OffsetRight = 0;
            closePanel.OffsetBottom = 0;
            AddChild(closePanel);

            var closeButton = new Button();
            closeButton.Text = TranslationServer.Translate("ui/inventory/close");
            closeButton.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            closeButton.Pressed += () =>
            {
                PlaySound("ui/close_click");
                Hide();
            };
            closePanel.AddChild(closeButton);
        }

        /// <summary>
        /// Resets selection and rebuilds the item grid from the current
        /// inventory state each time the panel is shown.
        /// </summary>
        protected override void ShowConstruct()
        {
            ClearSelection();
            RebuildGrid();
        }

        /// <summary>
        /// Clears the grid, selection, and any open target panel when
        /// the panel is hidden.
        /// </summary>
        protected override void HideDestruct()
        {
            ClearGrid();
            ClearSelection();
            HideTargetPanel();
        }

        /// <summary>
        /// Rebuilds the entire grid from the inventory, preserving the
        /// current selection by item ID if the item still exists.
        /// Called automatically by <see cref="IInventory.Change"/>.
        /// </summary>
        public void Refresh()
        {
            if (!Visible)
                return;

            var prevSelectedId = _selectedContainer?.Item?.ID;
            ClearGrid();
            ClearSelection();
            RebuildGrid();

            if (prevSelectedId != null)
            {
                var container = GameData.Instance.Runtime.Inventory.TryGet(prevSelectedId);
                if (container != null)
                    SelectItem(container);
            }
        }

        private void RebuildGrid()
        {
            ClearGrid();

            var items = GameData.Instance.Runtime.Inventory.Items;
            if (items.Count == 0 || _gridContent == null)
                return;

            var cellSize = CalculateCellSize();

            for (int i = 0; i < items.Count; i++)
            {
                var container = items[i];
                var cell = _itemScene.Instantiate<InventoryItemBehaviour>();
                cell.MouseFilter = MouseFilterEnum.Stop;
                cell.Modulate = NormalColor;

                var captured = container;
                cell.MouseEntered += () =>
                {
                    if (cell != _selectedCell)
                        cell.Modulate = HoverColor;
                };
                cell.MouseExited += () =>
                {
                    if (cell != _selectedCell)
                        cell.Modulate = NormalColor;
                };
                cell.GuiInput += (InputEvent e) =>
                {
                    if (e is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
                    {
                        SelectItem(captured);
                        PlaySound("ui/skip");
                        AcceptEvent();
                    }
                };

                cell.Construct(container);
                _gridContent.AddChild(cell);
                PlaceCell(cell, i, cellSize, cellSize);
                _itemCells.Add(cell);
            }

            if (_itemCells.Count > 0)
            {
                var lastCell = _itemCells[_itemCells.Count - 1];
                var lastRow = lastCell.Position.Y + lastCell.Size.Y;
                _gridContent.CustomMinimumSize = new Vector2(0, lastRow + CellPadding);
            }
        }

        private float CalculateCellSize()
        {
            var viewportWidth = GetViewportRect().Size.X;
            if (viewportWidth <= 0)
                viewportWidth = 1920f;
            var contentWidth = viewportWidth - 40f;
            var leftPanelWidth = contentWidth * 3f / 5f - 5f;
            return (leftPanelWidth - (Columns - 1) * CellPadding) / Columns;
        }

        private void PlaceCell(InventoryItemBehaviour cell, int index, float cellW, float cellH)
        {
            var col = index % Columns;
            var row = index / Columns;
            var x = col * (cellW + CellPadding);
            var y = row * (cellH + CellPadding);
            cell.Position = new Vector2(x, y);
            cell.Size = new Vector2(cellW, cellH);
        }

        private void ClearGrid()
        {
            foreach (var cell in _itemCells)
                cell.QueueFree();
            _itemCells.Clear();
        }

        private void SelectItem(IItemContainer container)
        {
            ClearSelection();
            HideTargetPanel();

            _selectedContainer = container;
            var item = container.Item;

            foreach (var cell in _itemCells)
            {
                if (cell.Model == container)
                {
                    _selectedCell = cell;
                    cell.Modulate = SelectedColor;
                    break;
                }
            }

            _itemName.Text = TranslationServer.Translate(item.Name);
            _itemName.Visible = true;

            _itemDescription.Text = TranslationServer.Translate(item.Description);
            _itemDescription.Visible = true;

            _itemCount.Text = "×" + container.Count.ToString();
            _itemCount.Visible = true;

            var isUsable = (item.ItemType.HasFlag(ItemType.Used) || item.ItemType.HasFlag(ItemType.TargetUsed))
                && item.ItemAction != null;
            _useButton.Visible = isUsable;

            var canDrop = !item.ItemType.HasFlag(ItemType.CantDrop);
            _dropButton.Visible = canDrop;

            _quantityContainer.Visible = false;
            _messageLabel.Visible = false;
        }

        private void ClearSelection()
        {
            if (_selectedCell != null)
            {
                _selectedCell.Modulate = NormalColor;
                _selectedCell = null;
            }

            _selectedContainer = null;

            _itemName.Visible = false;
            _itemDescription.Visible = false;
            _itemCount.Visible = false;
            _useButton.Visible = false;
            _dropButton.Visible = false;
            _quantityContainer.Visible = false;
            _messageLabel.Visible = false;
        }

        private void OnUseClicked()
        {
            if (_selectedContainer == null)
                return;

            var item = _selectedContainer.Item;

            if (item.ItemType.HasFlag(ItemType.TargetUsed))
            {
                ShowCharacterTargetPanel();
                return;
            }

            if (!TryConsumeActionPoint())
                return;

            item.ItemAction.UseAction(item);
            GameData.Instance.Runtime.Inventory.Remove(item, 1);
            PlaySound("ui/cook");
        }

        private void OnDropClicked()
        {
            if (_selectedContainer == null)
                return;

            _dropQuantity = 1;
            _quantitySlider.MaxValue = _selectedContainer.Count;
            _quantitySlider.Value = 1;
            UpdateQuantityLabel();
            _quantityContainer.Visible = true;
        }

        private void OnQuantitySliderChanged(double value)
        {
            _dropQuantity = Mathf.RoundToInt((float)value);
            UpdateQuantityLabel();
        }

        private void OnPlusClicked()
        {
            if (_selectedContainer == null)
                return;
            _dropQuantity = Mathf.Min(_dropQuantity + 1, _selectedContainer.Count);
            _quantitySlider.Value = _dropQuantity;
        }

        private void OnMinusClicked()
        {
            _dropQuantity = Mathf.Max(_dropQuantity - 1, 1);
            _quantitySlider.Value = _dropQuantity;
        }

        private void OnMaxClicked()
        {
            if (_selectedContainer == null)
                return;
            _dropQuantity = _selectedContainer.Count;
            _quantitySlider.Value = _dropQuantity;
        }

        private void OnDropConfirm()
        {
            if (_selectedContainer == null)
                return;

            if (!TryConsumeActionPoint())
                return;

            var item = _selectedContainer.Item;
            GameData.Instance.Runtime.Inventory.Remove(item, _dropQuantity);
            PlaySound("ui/close_click");
            _quantityContainer.Visible = false;
        }

        private void UpdateQuantityLabel()
        {
            var qtyText = TranslationServer.Translate("ui/inventory/drop_quantity");
            _quantityLabel.Text = qtyText + ": " + _dropQuantity.ToString() + "/" + _selectedContainer.Count.ToString();
        }

        /// <summary>
        /// Checks whether the action can proceed in the current battle
        /// context. Outside battle (no <see cref="BattleSceneContext"/>
        /// or <see cref="BattleSceneContext.IsDestroyed"/>) returns
        /// <c>true</c> immediately — actions are free. Inside an active
        /// battle when <see cref="BattleSceneContext.ActiveCharacter"/>
        /// is set, consumes 1 action point from that character. If
        /// insufficient AP, displays an error message and returns
        /// <c>false</c>.
        /// </summary>
        private bool TryConsumeActionPoint()
        {
            if (BattleSceneContext.Instance == null || BattleSceneContext.Instance.IsDestroyed)
                return true;

            var character = BattleSceneContext.Instance.ActiveCharacter;
            if (character == null)
                return true;

            if (character.Attributes.Point.Value < 1)
            {
                ShowMessage(TranslationServer.Translate("ui/inventory/no_ap"));
                PlaySound("ui/close_click");
                return false;
            }

            character.Attributes.Point.Value -= 1;
            return true;
        }

        private void ShowCharacterTargetPanel()
        {
            HideTargetPanel();

            _targetPanel = new CharacterTargetPanel();
            _targetPanel.CharacterSelected += OnTargetSelected;
            _targetPanel.Cancelled += HideTargetPanel;
            _rightPanel.AddChild(_targetPanel);
        }

        private void HideTargetPanel()
        {
            if (_targetPanel != null)
            {
                _targetPanel.CharacterSelected -= OnTargetSelected;
                _targetPanel.Cancelled -= HideTargetPanel;
                _targetPanel.QueueFree();
                _targetPanel = null;
            }
        }

        private void OnTargetSelected(CharacterInfoData info)
        {
            if (_selectedContainer == null)
                return;

            if (!TryConsumeActionPoint())
            {
                HideTargetPanel();
                return;
            }

            var item = _selectedContainer.Item;
            item.ItemAction.UseAction(item);
            GameData.Instance.Runtime.Inventory.Remove(item, 1);
            PlaySound("battle/card_pick");
            HideTargetPanel();
        }

        private void ShowMessage(string text)
        {
            _messageLabel.Text = text;
            _messageLabel.Visible = true;
        }

        private void PlaySound(string id)
        {
            var audio = SceneContextNode.FindNode<AudioService>("AudioService");
            audio?.PlaySound(id, SoundType.UI);
        }
    }
}
