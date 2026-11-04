# Save Data Model

## Save file structure (SavePackage)

| Field | Type | Description |
|---|---|---|
| `SaveId` | `string` | User-facing save name (e.g. "slot_1") |
| `Timestamp` | `DateTime` | UTC timestamp when the save was created |
| `CoreVersion` | `string` | Version of the game core that created this save |
| `ModData` | `Dict<string, ModSlot>` | Core and active mod data slots |
| `Game` | `SaveGameInfo` | Game ID/name/version and ordered `Mods` (`Id`, `Version`) snapshot; null for unscoped development saves |
| `Dormant` | `Dict<string, ModSlot>` | Inactive/incompatible slots preserved for a later return |

## `"__core__"` slot structure

The core slot uses these blob keys:

| Blob key | Content type | Description |
|---|---|---|
| `Time` | `long` | In-game clock tick (minutes elapsed) |
| `Lamp` | key-value | `Radius` (float), `Influence` (float) |
| `ActionPool` | `List<string>` | Persistent action card pool IDs |
| `Skills` | `List<string>` | Owned skill IDs |
| `CompletedScenarios` | `List<string>` | Completed scene-chain scenario IDs |
| `MapZones` | `MapZoneStateData` | World-map open/visible overrides against authored defaults |
| `Flags` | `Dictionary<string,string>` | Global flags and scoped `<map>/<event>:<name>` flags |

## Mod-owned slots

| Slot | Blob keys | Owner |
|---|---|---|
| `characters_base` | `Characters`, `BattleSet` | Party snapshots and active combat-party IDs |
| `items_base` | `Inventory`, `Recipes` | Item id/count pairs (including unresolved entries) and learned recipes |
| `quests_base` | `Quests` | Quest state, including unresolved records |
| `rpg` | `Position` | RPG map and position state |
| `stats_base` | `Stats` | Stats sheets and progression |
| `equipment` | `Worn` | Equipped item state |
| `recruiting` | `Recruits` | Recruit roster |
| `npc_barks_base` | `History` | Completed ambient definitions and remaining cooldowns |

These slots are written by the owning mods' `IModSaveable` implementations; they are not part of `__core__`. Current loading does not migrate former core-owned character/item/quest blobs automatically. A currently running battle, dialog execution pointer, speech bubble or transient speech event is not a saved snapshot.

Slot preview metadata (`CurrentSceneName`, `GameTime`, `CharacterCount`) is stored in `ModMetadata.CustomData` as a JSON object, so the slot list UI can read it without deserialising game-state blobs.

Subsystems **not persisted** by `BuildCoreSlot` today: there are no blob keys for the active battle encounter (held by the turn-battle mod in `BattleRequest.Current`), so an encounter in progress resets between sessions. The recruit roster is not a core blob either: it is persisted by the `recruiting` mod in its own `ModSlot` through `IModSaveable`.

The saved `CurrentSceneName` is used for the slot-list UI and scene navigation after loading; `RuntimeData.CurrentScene` itself is not stored — it is a read-only property resolved live from the `SceneManager`/current scene at runtime.

## ModSlot

| Field | Type | Description |
|---|---|---|
| `Metadata` | `ModMetadata` | Structured metadata for core validation |
| `Data` | `Dict<string, DataBlob>` | Named data blocks defined by the mod |

## ModMetadata

The core pre-fills `ModId`, `ModName`, and `ModVersion` from `IModInfo` before calling `OnSave`. The mod only needs to set `MinCoreVersion`, `CustomData`, and the data blobs.

| Field | Type | Set by | Description |
|---|---|---|---|
| `ModId` | `string` | core | Mod ID (from `IModInfo.Id`) |
| `ModName` | `string` | core | Human-readable name (from `IModInfo.DisplayTitle`) |
| `ModVersion` | `string` | core | Version of the installed mod (from `IModInfo.Version`, read from `manifest.json`) |
| `MinCoreVersion` | `string` | mod | Minimum core version required |
| `Dormant` | `bool` | core | True while the slot is retained inactive |
| `DormantReason` | `string` | core | `NotLoaded`, `Downgrade`, `CoreTooOld`, or `LoadFailed` |
| `DormantSince` | `DateTime?` | core | First dormancy date |
| `CustomData` | `string` | mod | Free-form JSON for extra metadata |

## DataBlob

A versioned byte container with three access modes:

| Mode | Methods | Use case |
|---|---|---|
| **Key-value** | `GetInt/SetInt`, `GetFloat/SetFloat`, `GetString/SetString`, `GetBool/SetBool` | Simple fields like HP, mana, lamp radius |
| **JSON** | `ReadJson<T>()`, `WriteJson<T>(data)` | Complex objects with versioned schemas |
| **Binary** | `GetBytes()`, `SetBytes(byte[])` | Raw data (textures, compressed payloads) |

Key-value and JSON modes conflict — calling `WriteJson` overwrites the internal dictionary, and calling `SetInt` after `WriteJson` replaces the JSON object with a dictionary. Choose one mode per blob.

### DataBlob constructors

```csharp
new DataBlob();                          // version=1, empty
new DataBlob(version: 2);               // version=2, empty
new DataBlob(version: 2, myObject);      // version=2, serialises myObject as JSON byte payload
new DataBlob(rawBytes);                  // version=1, stores raw bytes
```

The raw payload is also exposed directly via the public `Payload` property (`byte[]`; assigning `null` is coerced to an empty array). Unlike `GetBytes()`, reading `Payload` does **not** flush pending key-value entries into the byte array first.

## SaveSlotInfo

Returned by `SaveService.GetSaveSlots(slotCount)` for the load-game UI. There is no numeric slot ID/index field — the logical slot identity is `FileName` (e.g. `slot_1`).

| Field | Type | Description |
|---|---|---|
| `FileName` | `string` | Logical slot name (e.g. `slot_1`), used as the save-file identifier |
| `SaveTime` | `DateTime` | UTC timestamp from `SavePackage.Timestamp` (`DateTime.MinValue` for empty slots) |
| `SceneName` | `string` | Scene at save time (from the core slot preview; `"unknown"` fallback) |
| `ScreenshotPath` | `string` | Godot path of the thumbnail PNG (e.g. `user://saves/<game_id>/slot_1.png`) |
| `HasScreenshot` | `bool` | `true` if a thumbnail PNG exists on disk |
| `IsEmpty` | `bool` | `true` when no JSON save file exists for this slot |
| `GameTime` | `long` | In-game time tick at save moment |
| `CharacterCount` | `int` | Number of characters at save moment |
| `GameId`, `GameName` | `string` | Game identity and saved display name |
| `Mods` | `List<SaveGameMod>` | Saved mod-composition snapshot |
| `LampPercent` | `float` | **Known placeholder**: currently always written as `0f` by `GetSaveSlots` — no real lamp data is computed yet |
