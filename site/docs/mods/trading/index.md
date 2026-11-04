# Shops and trading

`trading` adds the entity kind `shop` and the action `trade.open`. Currency is an ordinary inventory item; the default is `money/coin`, defined by this mod. A shop has unlimited stock and no persistent stock state. Sold items do not become its new offers.

Put a shop in `shops/` and depend on `trading` and the mods owning its goods:

```json
{
  "Kind": "shop",
  "Items": [{
    "Id": "my_game/merchant", "Name": "my_game/merchant/name",
    "Currency": "money/coin",
    "Sells": [{"Item": "rpg_plus/elixir", "Price": 10}],
    "Buys": [{"Item": "rpg_plus/branches", "Price": 1}]
  }]
}
```

`Name` is a locale key. `Sells` lists what the player buys; `Buys` lists what the merchant accepts. Prices are positive integers per unit; prices below 1 are ignored. The example goods are owned by `rpg_plus_game`.

```scenario
action trade.open shop=my_game/merchant
end
```

The trade window has the merchant's offers on the left and the player's bag on the right. Pick an item, set the quantity with the slider, −/+, or All, and press Buy or Sell. Buy quantities are limited by available currency; sell quantities by the bag. Other bag entries cannot be sold here. Walking is blocked while trading; Esc closes the window.

RPG Plus supplies the concrete shop `rpg_plus/village_merchant` in `mods/rpg_plus_game/shops/village_merchant.json`. To extend an existing shop, use `patches/shop/<your_mod>.json` and a dependency on its owner. Offers have no declared merge key. To update one offer, supply an explicit operation such as `"Sells$add": {"$key": "Item", "$items": [{"Item": "rpg_plus/elixir", "Price": 12}]}`. A plain `$add` appends instead; see [Array patches](../patches/arrays-and-identity.md).

Only inventory changes persist, in the `items_base` save slot. Data Editor and the command palette consume `mods/trading/schemas/trading.json`; implementation is in `mods/trading/src/`.
