# Test Defaults and Patches — プロパティパッチのテストフィクスチャ

この MOD はコンテンツではありません。**テストフィクスチャ**です。`property_patches` テストが読み込む小さなプロパティパッチの集まりで、他の MOD のキャラクターやアイテムへのパッチが、ゲームが約束したとおりに合成されるかを確かめます。ゲームの一部になることはなく、プレイヤーが有効にすることも想定していません。

## 何をパッチするか

プロパティパッチは `patches/<kind>.json` というファイルです。各エントリーは `Id` でエンティティを指名し、追加・変更するプロパティを並べます。`Id` が `*` なら**ワイルドカード**で、その種類の全エンティティに届きます。フィクスチャには種類ごとに、対象指定のパッチとワイルドカードのパッチが1つずつあります：

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

これらのパッチが確かめる規則：

- **対象指定**のパッチはワイルドカードに勝つ：Marao の `TestDefault` は -20 ではなく 100 になる；
- **ワイルドカード**は残りすべてのエンティティに届く：他のキャラクターの `TestDefault` は -20、すべてのアイテムの `Durability` は 50 になる；
- 片方だけが設定するキーはすべて残る：Marao にはさらに `TestGlobalPatch` 1 が、レーションには `GlobalItemProp` がつく；
- 対象指定のキーが他のエンティティに漏れることはない；
- エンティティ自身の型付きフィールド（レーションの名前など）は合成を生き残る。

## テストの実行

```text
godot --path . res://scenes/menu/launcher.tscn -- --with-mods=test_defaults_and_patches --itest=property_patches
dotnet run --project tests/Cthangover.TestRunner -- --test property_patches
```

フィクスチャが読み込まれるのは、`--with-mods=` が名指しするからです。実際の実行での出力：

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

テストは MOD の一部（`src/PropertyPatchesTest.cs`）で、`--itest=property_patches` のときだけ有効になります。

## なぜゲームに入らないのか

マニフェストには `"fixture": true` と書かれています。フィクスチャは `--all-mods` の対象にも、ゲームの MOD セットにも決して入りません。`--with-mods=` で名指しされたときだけ読み込まれるため、そのパッチが他のテストや本番に漏れることはありません。本物のゲームで有効にすれば、Cthangover の全キャラクターと全アイテムにテスト用のプロパティを足すことになります——ワイルドカードパッチはすべてに触れるからです。依存先はパッチ対象の Cthangover MOD です（`cthangover_game`、`cthangover_murakami`、`cooking`）。

同じ MOD は、ツール系テストが書き込んでも安全な場所にもなっています：`tools_windows` テストはシーンエディターでここにシーンを作り、また削除します。
