# Test Defaults and Patches — 属性补丁的测试夹具

这个模组不是内容，而是一个**测试夹具**：一小批属性补丁，由 `property_patches` 测试加载，用来检查对其他模组角色和物品的补丁是否按游戏承诺的方式合并。它永远不会属于任何游戏，也不该由玩家来启用。

## 它补丁了什么

属性补丁是一个 `patches/<kind>.json` 文件。每条记录用 `Id` 指明一个实体，并列出要添加或修改的属性；`Id` 为 `*` 时是**通配符**，能命中该类型的每个实体。夹具对每个类型各备有一条定向补丁和一条通配补丁：

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

这些补丁验证的规则：

- **定向**补丁胜过通配符：Marao 的 `TestDefault` 是 100，而不是 -20；
- **通配符**命中其余所有实体：任何其他角色的 `TestDefault` 都是 -20，每个物品的 `Durability` 都是 50；
- 只由其中一方设置的键全部保留：Marao 还会得到 `TestGlobalPatch` 1，口粮会得到 `GlobalItemProp`；
- 定向键绝不会泄漏到其他实体；
- 实体自己的类型化字段——比如口粮的名字——在合并后原样保留。

## 运行测试

```text
godot --path . res://scenes/menu/launcher.tscn -- --with-mods=test_defaults_and_patches --itest=property_patches
dotnet run --project tests/Cthangover.TestRunner -- --test property_patches
```

只有 `--with-mods=` 点名时，夹具才会被加载。一次真实运行的输出：

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

测试代码在模组内（`src/PropertyPatchesTest.cs`），只在 `--itest=property_patches` 时激活。

## 为什么它不进游戏

它的清单里写着 `"fixture": true`。夹具永远不会进入 `--all-mods` 或某个游戏的模组集；只有 `--with-mods=` 点名时才会加载，因此它的补丁不可能泄漏到其他测试或实际游戏中。要是在真实游戏里启用，它会给 Cthangover 的每个角色和每个物品加上测试属性——通配补丁波及一切。它依赖自己所补丁的 Cthangover 模组（`cthangover_game`、`cthangover_murakami`、`cooking`）。

这个模组也是工具测试的安全落笔之处：`tools_windows` 测试会通过场景编辑器在其中创建一个场景，随后再删除。
