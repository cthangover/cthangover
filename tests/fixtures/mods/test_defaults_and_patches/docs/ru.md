# Test Defaults and Patches — тестовая фикстура для патчей свойств

Этот мод — не игровой контент. Это **тестовая фикстура**: небольшой набор патчей свойств, который загружает тест `property_patches`, чтобы проверить, что патчи чужих персонажей и предметов складываются так, как игра обещает. Он никогда не входит в игры и не рассчитан на включение игроками.

## Что он патчит

Патч свойств — это файл `patches/<kind>.json`. Каждая запись называет сущность по `Id` и перечисляет свойства, которые нужно добавить или изменить; `Id` `*` — это **подстановочный знак**, достающий каждую сущность данного вида. В фикстуре на каждый вид есть один точечный патч и один wildcard-патч:

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

Правила, которые эти патчи проверяют:

- **точечный** патч сильнее подстановочного: у Marao `TestDefault` равен 100, а не -20;
- **wildcard** достаёт все остальные сущности: у любого другого персонажа `TestDefault` равен -20, у каждого предмета `Durability` — 50;
- ключи, которые задаёт только один из них, сохраняются все: у Marao вдобавок появится `TestGlobalPatch` 1, у рациона — `GlobalItemProp`;
- точечный ключ никогда не протекает на другие сущности;
- собственные типизированные поля сущности — например, название рациона — переживают слияние.

## Как запустить тест

```text
godot --path . res://scenes/menu/launcher.tscn -- --with-mods=test_defaults_and_patches --itest=property_patches
dotnet run --project tests/Cthangover.TestRunner -- --test property_patches
```

Фикстура загружается только потому, что её называет `--with-mods=`. Вот её вывод в реальном запуске:

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

Тест лежит в самом моде (`src/PropertyPatchesTest.cs`) и активен только с `--itest=property_patches`.

## Почему он не попадает в игры

В его манифесте стоит `"fixture": true`. Фикстура никогда не входит в `--all-mods` и в набор модов игры; она загружается, только когда её назовут в `--with-mods=`, поэтому её патчи не могут протечь в другие тесты или в игру. Будь она включена в настоящей игре, она добавила бы тестовые свойства каждому персонажу и каждому предмету Cthangover — wildcard-патчи касаются всего. Зависит она от модов Cthangover, которые патчит (`cthangover_game`, `cthangover_murakami`, `cooking`).

Этот же мод — ещё и безопасное место для записи инструментальных тестов: тест `tools_windows` создаёт в нём сцену через редактор сцен и потом удаляет её.
