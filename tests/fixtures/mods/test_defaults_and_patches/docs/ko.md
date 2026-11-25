# Test Defaults and Patches — 속성 패치 테스트 픽스처

이 모드는 콘텐츠가 아니라 **테스트 픽스처**입니다. `property_patches` 테스트가 불러오는 작은 속성 패치 모음으로, 다른 모드의 캐릭터와 아이템에 대한 패치가 게임이 약속한 방식으로 합쳐지는지 확인합니다. 게임에 속하는 일이 없으며, 플레이어가 켜도록 만들어진 것도 아닙니다.

## 무엇을 패치하나

속성 패치는 `patches/<kind>.json` 파일입니다. 각 항목은 `Id`로 엔티티를 지목하고 추가하거나 바꿀 속성을 나열합니다. `Id`가 `*`이면 **와일드카드**로, 해당 종류의 모든 엔티티에 닿습니다. 픽스처에는 종류별로 대상 지정 패치 하나와 와일드카드 패치 하나가 있습니다:

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

이 패치들이 확인하는 규칙:

- **대상 지정** 패치는 와일드카드를 이긴다: Marao의 `TestDefault`는 -20이 아니라 100이 됩니다;
- **와일드카드**는 나머지 모든 엔티티에 닿는다: 다른 캐릭터의 `TestDefault`는 -20, 모든 아이템의 `Durability`는 50이 됩니다;
- 한쪽만 설정하는 키는 모두 보존된다: Marao에게는 `TestGlobalPatch` 1이, ration 아이템에는 `GlobalItemProp`이 추가로 붙습니다;
- 대상 지정 키가 다른 엔티티로 새어 나가지 않습니다;
- 엔티티 자신의 타입 필드(예: ration의 이름)는 병합 후에도 살아남습니다.

## 테스트 실행

```text
godot --path . res://scenes/menu/launcher.tscn -- --with-mods=test_defaults_and_patches --itest=property_patches
dotnet run --project tests/Cthangover.TestRunner -- --test property_patches
```

픽스처는 `--with-mods=`가 이름을 불러 줄 때만 로드됩니다. 실제 실행에서의 출력:

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

테스트는 모드 안에(`src/PropertyPatchesTest.cs`) 있으며 `--itest=property_patches`일 때만 활성화됩니다.

## 왜 게임에 들어가지 않나

매니페스트에는 `"fixture": true`라고 적혀 있습니다. 픽스처는 `--all-mods`에도, 게임의 모드 세트에도 절대 속하지 않습니다. `--with-mods=`가 이름을 불러야 로드되므로, 그 패치가 다른 테스트나 실제 플레이로 새어 들어갈 수 없습니다. 실제 게임에서 켜면 Cthangover의 모든 캐릭터와 모든 아이템에 테스트 속성을 붙이는 셈이 됩니다 — 와일드카드 패치는 모든 것을 건드리니까요. 의존 대상은 패치하는 Cthangover 모드들(`cthangover_game`, `cthangover_murakami`, `cooking`)입니다.

같은 모드는 도구 테스트가 안전하게 쓸 수 있는 장소이기도 합니다: `tools_windows` 테스트는 씬 편집기로 여기에 씬을 하나 만들었다가 다시 지웁니다.
