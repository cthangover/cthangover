# Action parameter catalog

This table follows the installed production `schemas/*.json`, including conditional integrations. Load the owning mod before using its action. Required parameters are bold; other parameters are optional. A schema is a validation/editor contract, not an implementation: the runtime action is still responsible for applying it. Test-only suites are excluded.

| Action | Owner | Parameters (schema type; default if given) |
|---|---|---|
| `battle.init` | `turn_battle_base` | **`enemies`** (ref_list:character); `scene` (scene) |
| `battle.init` | `turn_battle_base__quests` | **`enemies`** (ref_list:character); `scene` (scene); `quest_id` (ref:quest); `new_tag` (text) |
| `battle.set_core` | `turn_battle_base` | **`core`** (choice) |
| `character.add_skill` | `skills_base` | **`skill_id`** (ref:skill) |
| `character.send_notification` | `characters_base` | **`type`** (ref:character) |
| `craft.open` | `crafting` | **`station`** (ref:craft_station) |
| `equip.put` | `equipment` | **`item`** (ref:item); `who` (who); `slot` (ref:equip_slot); `give` (bool) |
| `equip.remove` | `equipment` | **`slot`** (ref:equip_slot); `who` (who) |
| `item.add` | `items_base` | **`id`** (ref:item); `count` (int; default `1`) |
| `item.remove` | `items_base` | **`id`** (ref:item); `count` (int; default `1`) |
| `map.close_zone` | `map_base` | **`zone_id`** (text) |
| `map.hide_zone` | `map_base` | **`zone_id`** (text) |
| `map.open_zone` | `map_base` | **`zone_id`** (text) |
| `map.show_zone` | `map_base` | **`zone_id`** (text) |
| `npc.look` | `npc_barks_base` | **`who`** (text); **`target`** (text) |
| `party.add` | `characters_base` | **`id`** (ref:character) |
| `quest.add_tag` | `quests_base` | **`quest_id`** (ref:quest); **`tag`** (text) |
| `quest.remove_tag` | `quests_base` | **`quest_id`** (ref:quest); **`tag`** (text) |
| `quest.send_notification` | `quests_base` | **`quest_id`** (ref:quest) |
| `quest.set_data_status` | `quests_base` | **`quest_id`** (ref:quest); **`level`** (int) |
| `quest.set_status` | `quests_base` | **`quest_id`** (ref:quest); **`status`** (choice) |
| `rpg.battle` | `rpg__battles` | **`enemies`** (ref_list:character); `win_flag` (flag) |
| `rpg.teleport` | `rpg` | `map` (ref:rpg_map); **`x`** (int); **`y`** (int); `dir` (choice) |
| `stat.add` | `stats_base` | **`stat`** (ref:stat); `value` (number; default `1`); `who` (who) |
| `stat.archetype` | `stats_base` | `id` (ref:archetype); `who` (who) |
| `stat.exp` | `stats_base` | **`value`** (int); `who` (who; default `party`) |
| `stat.restore` | `stats_base` | `who` (who; default `party`); `stat` (text) |
| `trade.open` | `trading` | **`shop`** (ref:shop) |

`add_live2d_model` is also registered by `live2d_base`; its runtime action requires `model_path=`, accepts `mod_id=`, `node_name=` (default `Live2DModel`), `x=`/`y=` together and `scale=` and it is not currently described by an action schema. `toggle_cooking_workbench` is the cooking convenience alias for its workbench panel; it has no parameters. Spatial `npc.look` requires an ambient execution host and cannot be used as a general modal action.

See [Action examples](index.md), [RPG](../../../rpg/), [Stats](../../../stats/), [Crafting](../../../crafting/), [Trading](../../../trading/) and [Background scenarios](../background.md) for behaviour and constraints. Unknown/missing parameters and references are checked by the command palette and validator using the same schema catalog.
