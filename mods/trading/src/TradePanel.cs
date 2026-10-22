namespace Cthangover.Mods.Trading
{
    using System.Collections.Generic;
    using Cthangover.Core.Audio;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Items;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;
    using Cthangover.Core.UI.Inventory;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Base.Lists;
    using Cthangover.Mods.Interface.Theming;
    using Godot;

    /// <summary>
    /// Trade window: two inventories side by side, the merchant's goods on the left
    /// and the player's bag on the right, with a deal card between them. It follows
    /// the inventory panel (same cells, same frame, same passport idea), so trading
    /// feels like a second bag rather than a new screen.
    /// <para/>
    /// The window is a hosted "once" overlay (<see cref="EntryId"/> in the manifest),
    /// built on first use and reused for every merchant: <see cref="TradeOpenAction"/>
    /// hands it a <see cref="ShopInfo"/> and shows it. Clicking a cell selects the item
    /// and fills the deal card with its price, a quantity slider limited by money (when
    /// buying) or by the stack (when selling) and the deal button. The arithmetic is
    /// <see cref="TradeLedger"/>; the window only redraws on
    /// <see cref="IInventory.Change"/>, so it also follows changes made elsewhere.
    /// The money item is hidden from the player's grid and shown in the header.
    /// <para/>
    /// The inventory subscription is made on show and dropped on hide, against the
    /// inventory instance of that moment: a new game or a load replaces the runtime
    /// and its inventory while this window lives on.
    /// </summary>
    public partial class TradePanel : Widget
    {
        /// <summary>Id of the UI registry entry (manifest <c>ui</c>).</summary>
        public const string EntryId = "trade";

        private enum Side
        {
            None,
            Merchant,
            Player,
        }

        private const int Columns = 4;
        private const float CellGap = 10f;
        private const float DealWidth = 400f;

        private static readonly List<ShopOffer> NoOffers = new();

        private readonly List<InventoryItemBehaviour> _merchantCells = new();
        private readonly List<InventoryItemBehaviour> _playerCells = new();

        private ShopInfo _shop;
        private IInventory _inventory;
        private Side _side = Side.None;
        private string _selectedId;
        private int _quantity = 1;
        private string _message;
        private bool _messageOk;

        private Label _title;
        private TextureRect _walletIcon;
        private Label _walletLabel;
        private Button _closeButton;
        private Label _merchantTitle;
        private Label _playerTitle;
        private Label _footer;
        private Control _merchantGrid;
        private Control _playerGrid;
        private VBoxContainer _dealCard;
        private Label _quantityLabel;
        private Label _totalLabel;

        /// <summary>
        /// Sets the merchant to trade with. Called before the window is shown; if it is
        /// already open (another merchant's dialog ended in it), it switches at once.
        /// </summary>
        public void SetShop(ShopInfo shop)
        {
            _shop = shop;
            ResetSelection();
            if (Visible)
                RebuildAll();
        }

        protected override void OnceConstruct()
        {
            MouseFilter = MouseFilterEnum.Stop;
            _title = GetNode<Label>("Panel/Margin/Root/Header/Title");
            _walletIcon = GetNode<TextureRect>("Panel/Margin/Root/Header/Wallet/WalletIcon");
            _walletLabel = GetNode<Label>("Panel/Margin/Root/Header/Wallet/WalletLabel");
            _closeButton = GetNode<Button>("Panel/Margin/Root/Header/CloseButton");
            _merchantTitle = GetNode<Label>("Panel/Margin/Root/Body/Merchant/MerchantTitle");
            _merchantGrid = GetNode<Control>("Panel/Margin/Root/Body/Merchant/MerchantScroll/MerchantGrid");
            _playerTitle = GetNode<Label>("Panel/Margin/Root/Body/Player/PlayerTitle");
            _playerGrid = GetNode<Control>("Panel/Margin/Root/Body/Player/PlayerScroll/PlayerGrid");
            _dealCard = GetNode<VBoxContainer>("Panel/Margin/Root/Body/Deal/DealScroll/DealCard");
            _footer = GetNode<Label>("Panel/Margin/Root/Footer");

            ApplyTheme();
            _closeButton.Pressed += OnClosePressed;
        }

        protected override void ShowConstruct()
        {
            if (_inventory != null)
                _inventory.Change -= OnInventoryChanged;
            _inventory = GameData.Instance.Runtime.Inventory;
            _inventory.Change += OnInventoryChanged;
            if (_side == Side.None)
                SelectFirstOffer();
            RebuildAll();
        }

        protected override void HideDestruct()
        {
            if (_inventory != null)
                _inventory.Change -= OnInventoryChanged;
            _inventory = null;
            ClearCells(_merchantCells);
            ClearCells(_playerCells);
            ClearChildren(_dealCard);
            ResetSelection();
        }

        protected override void OnInput(InputEvent @event)
        {
            if (Visible && @event.IsActionPressed("ui_cancel"))
            {
                OnClosePressed();
                GetViewport().SetInputAsHandled();
            }
        }

        private void ApplyTheme()
        {
            UiKit.ApplyPanelBackdrop(GetNode<ColorRect>("Backdrop"));

            var panelStyle = UiKit.Frame(8, UiTokens.Surface0, UiTokens.Line);
            panelStyle.ShadowColor = new Color(0f, 0f, 0f, 0.6f);
            panelStyle.ShadowSize = 18;
            panelStyle.ShadowOffset = new Vector2(0f, 8f);
            GetNode<Panel>("Panel").AddThemeStyleboxOverride("panel", panelStyle);
            GetNode<Panel>("Panel/Margin/Root/Rule").AddThemeStyleboxOverride("panel", UiKit.Frame(0, UiTokens.Line));

            var dealStyle = UiKit.Frame(6, UiTokens.SideBg, UiTokens.Line);
            dealStyle.ContentMarginLeft = 18;
            dealStyle.ContentMarginRight = 18;
            dealStyle.ContentMarginTop = 16;
            dealStyle.ContentMarginBottom = 16;
            GetNode<PanelContainer>("Panel/Margin/Root/Body/Deal").AddThemeStyleboxOverride("panel", dealStyle);

            _title.AddThemeColorOverride("font_color", UiTokens.InkBright);
            _walletLabel.AddThemeColorOverride("font_color", UiTokens.AccentBright);
            _merchantTitle.AddThemeColorOverride("font_color", UiTokens.Muted);
            _playerTitle.AddThemeColorOverride("font_color", UiTokens.Muted);
            _footer.AddThemeColorOverride("font_color", UiTokens.Faint);
            CthangoverTheme.ApplyIcon(_closeButton);

            _merchantTitle.Text = Tr("trading/ui/merchant_goods").ToUpperInvariant();
            _playerTitle.Text = Tr("trading/ui/your_bag").ToUpperInvariant();
            _footer.Text = Tr("trading/ui/hint");
        }

        private void RebuildAll()
        {
            if (_shop == null || _inventory == null)
                return;

            _title.Text = Tr(_shop.Name ?? _shop.ID).ToUpperInvariant();
            _walletIcon.Texture = ItemFactory.Instance.Get(_shop.Currency)?.Sprite;
            _walletLabel.Text = TradeLedger.Money(_inventory, _shop).ToString();

            var cellSize = CalculateCellSize();
            ClearCells(_merchantCells);
            foreach (var offer in _shop.Sells ?? NoOffers)
            {
                var item = offer == null ? null : ItemFactory.Instance.Get(offer.Item);
                if (item == null || offer.Price <= 0)
                    continue;
                var cell = AddCell(_merchantGrid, _merchantCells, new ItemContainer { Item = item, Count = 1 }, cellSize);
                cell.Decorate(PriceTag(offer.Price), TradeLedger.MaxBuy(_inventory, _shop, item.ID) == 0);
                cell.Clicked += OnMerchantCellClicked;
            }
            FitGrid(_merchantGrid, _merchantCells);

            ClearCells(_playerCells);
            foreach (var container in _inventory.Items)
            {
                if (container?.Item == null || container.Item.ID == _shop.Currency)
                    continue;
                var cell = AddCell(_playerGrid, _playerCells, container, cellSize);
                var offer = _shop.FindBuy(container.Item.ID);
                cell.Decorate(offer == null ? null : PriceTag(offer.Price), offer == null);
                cell.Clicked += OnPlayerCellClicked;
            }
            FitGrid(_playerGrid, _playerCells);

            if (_side == Side.Player && _inventory.CheckCount(_selectedId) == 0)
                ResetSelectionKeepMessage();
            MarkSelected();
            BuildDeal();
        }

        private InventoryItemBehaviour AddCell(Control grid, List<InventoryItemBehaviour> cells, IItemContainer container, float cellSize)
        {
            var cell = new InventoryItemBehaviour();
            cell.Construct(container);
            grid.AddChild(cell);
            var index = cells.Count;
            cell.Position = new Vector2(index % Columns * (cellSize + CellGap), index / Columns * (cellSize + CellGap));
            cell.Size = new Vector2(cellSize, cellSize);
            cells.Add(cell);
            return cell;
        }

        private static void FitGrid(Control grid, List<InventoryItemBehaviour> cells)
        {
            if (cells.Count == 0)
            {
                grid.CustomMinimumSize = Vector2.Zero;
                return;
            }
            var last = cells[cells.Count - 1];
            grid.CustomMinimumSize = new Vector2(0, last.Position.Y + last.Size.Y + CellGap);
        }

        private float CalculateCellSize()
        {
            var viewportWidth = GetViewportRect().Size.X;
            if (viewportWidth <= 0)
                viewportWidth = 1920f;
            var gridWidth = (viewportWidth - 80f - 48f - DealWidth - 28f) / 2f;
            return Mathf.Clamp((gridWidth - (Columns - 1) * CellGap - 14f) / Columns, 64f, 150f);
        }

        private void MarkSelected()
        {
            foreach (var cell in _merchantCells)
                cell.SetSelected(_side == Side.Merchant && cell.Model.Item.ID == _selectedId);
            foreach (var cell in _playerCells)
                cell.SetSelected(_side == Side.Player && cell.Model.Item.ID == _selectedId);
        }

        private void BuildDeal()
        {
            ClearChildren(_dealCard);
            _quantityLabel = null;
            _totalLabel = null;

            var item = _side == Side.None ? null : ItemFactory.Instance.Get(_selectedId);
            if (item == null)
            {
                _dealCard.AddChild(UiKit.EmptyState(Tr("trading/ui/select"), 14));
                AddMessage();
                return;
            }

            var buying = _side == Side.Merchant;
            var offer = buying ? _shop.FindSell(item.ID) : _shop.FindBuy(item.ID);
            var max = buying ? TradeLedger.MaxBuy(_inventory, _shop, item.ID) : TradeLedger.MaxSell(_inventory, _shop, item.ID);
            _quantity = Mathf.Clamp(_quantity, 1, Mathf.Max(1, max));

            var header = UiKit.Row(16);
            var iconFrame = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
            iconFrame.AddThemeStyleboxOverride("panel", UiKit.Frame(8, UiTokens.Surface1, UiTokens.LineSoft));
            iconFrame.AddChild(UiKit.Icon(item.Sprite, 88));
            header.AddChild(iconFrame);
            var titles = UiKit.Column(8);
            titles.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            var name = UiKit.Label(Tr(item.Name), 22, UiTokens.InkBright);
            name.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            titles.AddChild(name);
            var badge = UiKit.Badge(Tr(buying ? "trading/ui/badge_merchant" : "trading/ui/badge_yours"),
                buying ? UiTokens.Accent : UiTokens.Ok);
            badge.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
            titles.AddChild(badge);
            header.AddChild(titles);
            _dealCard.AddChild(header);

            var description = UiKit.Label(Tr(item.Description), 14, UiTokens.Body);
            description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _dealCard.AddChild(description);

            var stats = UiKit.Row(8);
            if (offer != null)
                stats.AddChild(UiKit.StatChip(Tr("trading/ui/unit_price"), offer.Price.ToString(), UiTokens.AccentBright));
            stats.AddChild(buying
                ? UiKit.StatChip(Tr("trading/ui/wallet"), TradeLedger.Money(_inventory, _shop).ToString())
                : UiKit.StatChip(Tr("trading/ui/you_have"), _inventory.CheckCount(item.ID).ToString()));
            _dealCard.AddChild(stats);

            if (offer == null || max == 0)
            {
                var reason = offer == null ? "trading/ui/not_bought" : (buying ? "trading/ui/no_money" : "trading/ui/nothing_to_sell");
                var hint = UiKit.Label(Tr(reason), 13, UiTokens.Muted);
                hint.AutowrapMode = TextServer.AutowrapMode.WordSmart;
                _dealCard.AddChild(hint);
                AddMessage();
                return;
            }

            _dealCard.AddChild(BuildQuantity(max));
            _totalLabel = UiKit.Label(string.Empty, 18, UiTokens.AccentBright);
            _dealCard.AddChild(_totalLabel);
            UpdateQuantityLabels(max, offer.Price);

            var deal = MakeButton(Tr(buying ? "trading/ui/buy" : "trading/ui/sell"), true);
            deal.CustomMinimumSize = new Vector2(0, 44);
            deal.Pressed += OnDealPressed;
            _dealCard.AddChild(deal);
            AddMessage();
        }

        private Control BuildQuantity(int max)
        {
            var column = UiKit.Column(8);
            var head = UiKit.Row(8);
            var caption = UiKit.Label(Tr("trading/ui/quantity"), 12, UiTokens.Muted);
            caption.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            head.AddChild(caption);
            _quantityLabel = UiKit.Label(string.Empty, 12, UiTokens.AccentBright);
            head.AddChild(_quantityLabel);
            column.AddChild(head);

            var slider = new HSlider
            {
                MinValue = 1,
                MaxValue = Mathf.Max(1, max),
                Step = 1,
                Value = _quantity,
                Editable = max > 1,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                FocusMode = FocusModeEnum.None,
            };
            slider.ValueChanged += OnSliderChanged;
            column.AddChild(slider);

            var buttons = UiKit.Row(6);
            buttons.AddChild(MakeSmallButton("−", false));
            buttons.AddChild(MakeSmallButton("+", true));
            var all = MakeButton(Tr("trading/ui/all"), false);
            all.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            all.Pressed += OnAllPressed;
            buttons.AddChild(all);
            column.AddChild(buttons);
            return column;
        }

        private Button MakeSmallButton(string text, bool plus)
        {
            var button = MakeButton(text, false);
            button.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            if (plus)
                button.Pressed += OnPlusPressed;
            else
                button.Pressed += OnMinusPressed;
            return button;
        }

        // The inventory passport's button look (same tokens), so both windows match.
        private static Button MakeButton(string text, bool primary)
        {
            var button = new Button { Text = text, FocusMode = FocusModeEnum.None };
            button.AddThemeFontSizeOverride("font_size", 14);
            button.AddThemeStyleboxOverride("normal", ButtonStyle(primary, false));
            button.AddThemeStyleboxOverride("hover", ButtonStyle(primary, true));
            button.AddThemeStyleboxOverride("pressed", ButtonStyle(primary, true));
            button.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
            button.AddThemeColorOverride("font_color", primary ? UiTokens.AccentBright : UiTokens.Body);
            button.AddThemeColorOverride("font_hover_color", primary ? UiTokens.AccentHover : UiTokens.InkBright);
            button.AddThemeColorOverride("font_pressed_color", primary ? UiTokens.AccentHover : UiTokens.InkBright);
            return button;
        }

        private static StyleBoxFlat ButtonStyle(bool primary, bool hover)
        {
            var background = primary
                ? (hover ? UiTokens.BtnPrimaryHover : UiTokens.BtnPrimary)
                : (hover ? UiTokens.BtnHover : UiTokens.Surface3);
            var border = primary
                ? (hover ? UiTokens.AccentLineStrong : UiTokens.Alpha(UiTokens.Accent, 0.55f))
                : (hover ? UiTokens.Alpha(UiTokens.Accent, 0.55f) : UiTokens.Line);
            var style = UiKit.Frame(4, background, border);
            style.ContentMarginLeft = 12;
            style.ContentMarginRight = 12;
            style.ContentMarginTop = 10;
            style.ContentMarginBottom = 10;
            return style;
        }

        private void AddMessage()
        {
            if (string.IsNullOrEmpty(_message))
                return;
            var accent = _messageOk ? UiTokens.Ok : UiTokens.DangerText;
            var frame = UiKit.Frame(5, UiTokens.Alpha(accent, 0.1f), UiTokens.Alpha(accent, 0.5f));
            frame.ContentMarginLeft = 12;
            frame.ContentMarginRight = 12;
            frame.ContentMarginTop = 9;
            frame.ContentMarginBottom = 9;
            var wrapper = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
            wrapper.AddThemeStyleboxOverride("panel", frame);
            var label = UiKit.Label(_message, 13, accent);
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            wrapper.AddChild(label);
            _dealCard.AddChild(wrapper);
        }

        private void UpdateQuantityLabels(int max, int price)
        {
            if (_quantityLabel != null)
                _quantityLabel.Text = _quantity + " / " + max;
            if (_totalLabel != null)
                _totalLabel.Text = string.Format(Tr("trading/ui/total"), price * _quantity);
        }

        private int CurrentMax()
        {
            return _side == Side.Merchant
                ? TradeLedger.MaxBuy(_inventory, _shop, _selectedId)
                : TradeLedger.MaxSell(_inventory, _shop, _selectedId);
        }

        private void SetQuantity(int quantity)
        {
            var max = CurrentMax();
            var clamped = Mathf.Clamp(quantity, 1, Mathf.Max(1, max));
            if (clamped == _quantity)
                return;
            _quantity = clamped;
            BuildDeal();
        }

        private void OnSliderChanged(double value)
        {
            _quantity = Mathf.RoundToInt((float)value);
            var offer = _side == Side.Merchant ? _shop.FindSell(_selectedId) : _shop.FindBuy(_selectedId);
            UpdateQuantityLabels(CurrentMax(), offer?.Price ?? 0);
        }

        private void OnMinusPressed() => SetQuantity(_quantity - 1);

        private void OnPlusPressed() => SetQuantity(_quantity + 1);

        private void OnAllPressed() => SetQuantity(CurrentMax());

        private void OnDealPressed()
        {
            var item = ItemFactory.Instance.Get(_selectedId);
            if (item == null)
                return;

            var buying = _side == Side.Merchant;
            var offer = buying ? _shop.FindSell(item.ID) : _shop.FindBuy(item.ID);
            var count = _quantity;
            var ok = buying
                ? TradeLedger.Buy(_inventory, _shop, item.ID, count)
                : TradeLedger.Sell(_inventory, _shop, item.ID, count);

            _messageOk = ok;
            _message = ok
                ? string.Format(Tr(buying ? "trading/ui/bought" : "trading/ui/sold"), Tr(item.Name), count, (offer?.Price ?? 0) * count)
                : Tr("trading/ui/deal_failed");
            _quantity = 1;
            PlaySound(ok ? "items/use" : "ui/close_click");
            // The grids were already redrawn by OnInventoryChanged during the deal,
            // before the message existed; the card is redrawn again to show it.
            BuildDeal();
        }

        private void OnMerchantCellClicked(InventoryItemBehaviour cell) => Select(Side.Merchant, cell);

        private void OnPlayerCellClicked(InventoryItemBehaviour cell) => Select(Side.Player, cell);

        private void Select(Side side, InventoryItemBehaviour cell)
        {
            _side = side;
            _selectedId = cell.Model.Item.ID;
            _quantity = 1;
            _message = null;
            MarkSelected();
            BuildDeal();
            PlaySound("ui/skip");
        }

        private void SelectFirstOffer()
        {
            foreach (var offer in _shop?.Sells ?? NoOffers)
            {
                if (offer != null && offer.Price > 0 && ItemFactory.Instance.Get(offer.Item) != null)
                {
                    _side = Side.Merchant;
                    _selectedId = offer.Item;
                    return;
                }
            }
        }

        private void OnInventoryChanged(string id, int delta)
        {
            if (Visible)
                RebuildAll();
        }

        private void OnClosePressed()
        {
            PlaySound("ui/close_click");
            Hide();
        }

        private void ResetSelection()
        {
            ResetSelectionKeepMessage();
            _message = null;
        }

        private void ResetSelectionKeepMessage()
        {
            _side = Side.None;
            _selectedId = null;
            _quantity = 1;
        }

        private string PriceTag(int price) => string.Format(Tr("trading/ui/price_tag"), price);

        private static void ClearCells(List<InventoryItemBehaviour> cells)
        {
            foreach (var cell in cells)
                cell.QueueFree();
            cells.Clear();
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

        private static void PlaySound(string id)
        {
            var audio = SceneContextNode.FindNode<Node>("AudioService") as IAudioService;
            audio?.PlaySound(id, SoundType.UI);
        }
    }
}
