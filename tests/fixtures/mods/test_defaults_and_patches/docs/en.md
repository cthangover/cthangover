# Test Defaults and Patches — a test fixture for property patches

This mod is not content. It is a **test fixture**: a small set of property patches that the `property_patches` test loads to check that patches of other mods' characters and items merge the way the game promises. It is never part of a game and is not meant to be enabled by players.

## What it patches

A property patch is a file `patches/<kind>.json`. Each entry names an entity by its `Id` and lists properties to add or change; the `Id` `*` is a **wildcard** that reaches every entity of the kind. The fixture has one targeted and one wildcard patch per kind:

```json
{
  "Items": [
    { "Id": "Marao", "TestPatched": 42, "TestDefault": 100 },
    { "Id": "*", "TestDefault": -20, "TestGlobalPatch": 1 }
  ]
}
```

```json
{
  "Items": [
    { "Id": "food/ration", "Durability": 99, "SpecialProp": true },
    { "Id": "*", "Durability": 50, "GlobalItemProp": "patched" }
  ]
}
```

The rules these patches check:

- a **targeted** patch wins over the wildcard: Marao gets `TestDefault` 100, not -20;
- the **wildcard** reaches every other entity: any other character gets `TestDefault` -20, every item `Durability` 50;
- keys only one of them sets are all kept: Marao also gets `TestGlobalPatch` 1, the ration `GlobalItemProp`;
- a targeted key never leaks to other entities;
- the entity's own typed fields, such as the ration's name, survive the merge.

## Running the test

```text
godot --path . res://scenes/menu/launcher.tscn -- --with-mods=test_defaults_and_patches --itest=property_patches
dotnet run --project tests/Cthangover.TestRunner -- --test property_patches
```

The fixture is loaded only because `--with-mods=` names it. Its output in a real run:

```text
[PASS] Marao: targeted value: TestPatched=42
[PASS] Marao: targeted beats wildcard: TestDefault=100
[PASS] Marao: wildcard-only key: TestGlobalPatch=1
[PASS] werewolf_girl_1: wildcard value: TestDefault=-20
[PASS] werewolf_girl_1: no targeted key
[PASS] ration: targeted beats wildcard: Durability=99
[PASS] ration: targeted-only key: SpecialProp=true
[PASS] ration: wildcard-only key: GlobalItemProp=patched
[PASS] ration: typed field survives the merge
[PASS] wolf_meat: wildcard value: Durability=50
[TEST_RESULT] name=property_patches passed=14 failed=0
```

The test is part of the mod (`src/PropertyPatchesTest.cs`) and is active only with `--itest=property_patches`.

## Why it stays out of games

Its manifest says `"fixture": true`. A fixture is never part of `--all-mods` or of a game's mod set; it loads only when `--with-mods=` names it, so its patches cannot leak into other tests or into play. Enabled in a real game, it would add test properties to every character and every item of Cthangover — wildcard patches touch everything. It depends on the Cthangover mods it patches (`cthangover_game`, `cthangover_murakami`, `cooking`).

The same mod is also a safe place for the tool tests to write into: the `tools_windows` test creates a scene in it through the scene editor and removes it again.
