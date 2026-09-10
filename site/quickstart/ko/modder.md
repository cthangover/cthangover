# 레벨 2 — 모더: DSL 씬

<div class="qs-meta">
<span>JSON + DSL</span>
<span>15분</span>
<span>코드 불필요</span>
</div>

자체 씬이 있는 모드를 만듭니다 — 대화, 선택지, 전환이 있는 방입니다. 또한 원본 파일을 건드리지 않고 기존 주방 씬에 "선술집 가기" 옵션을 추가합니다.

---

## 1단계: 모드 구조

폴더와 기본 파일 생성:

```filestree
mods/
└── quickstart_modder/
    ├── manifest.json
    ├── scenes/
    │   └── my_alko_bar.json              # 새로운 장소 — 선술집!
    ├── scenarios/
    │   ├── my_alko_bar.scenario          # 선술집 내부 시나리오, 플레이어가 새 장소와 상호작용 가능
    │   └── town_entry_override.scenario  # 마을 씬을 재정의하여 새 장소로의 전환 추가
    ├── backgrounds/
    │   └── my_alko_bar/
    │       └── bg.png                    # 선술집 내부 이미지
    ├── backgrounds/
    │   └── sounds/
    │       └── alko_bar_ambient.ogg      # 선술집 배경 소음
    └── locale/
        └── ru.properties                 # 러시아어 로컬라이제이션
```

---

## 2단계: manifest.json

`mods/quickstart_modder/manifest.json`：

```jsonc
{
  "id": "alko_bar",                     // 모드 ID
  "name": "내 선술집",                   // 모드 메뉴에 표시되는 모드 이름
  "description": "마을에 선술집 씬 추가",  // 모드 메뉴에 표시되는 모드 설명
  "author": "modder",                   // 모드 작성자
  "depends": ["core"]                   // 코어 게임 의존성 (수동으로 모드 로드 순서를 정할 필요 없음)
}
```

---

## 3단계: 씬 정의

`mods/quickstart_modder/scenes/my_alko_bar.json`：

```jsonc
{
  "name": "my_alko_bar",                              // 새로운 고유 씬 식별자 생성 (switch_scene에서 사용), 이것이 새 씬이 됩니다.
  "defaultBackground": "my_alko_bar/bg",              // backgrounds/ 내 PNG 배경 경로 (확장자 없음)
  "defaultAmbient": "locations/alkobar/bar_ambient",  // 이 장소의 환경음 ID (확장자 없음), 예: 씬에 들어갈 때 재생되는 "술집 소음"
  "defaultScenario": "scenarios/my_alko_bar.scenario" // 다른 것이 지정되지 않은 경우 이 씬에서 실행될 기본 시나리오
}
```

| 필드 | 설명 |
|---|---|
| `name` | 고유 씬 ID. `switch_scene`에서 사용 |
| `defaultBackground` | `backgrounds/` 내 배경 경로 (확장자 없음) |
| `defaultAmbient` | 환경음. 빈 문자열 — 소리 없음 |
| `defaultScenario` | 씬 진입 시 실행될 `.scenario` 파일 경로 |

---

## 4단계: 배경

`mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`에 이미지를 배치하세요.

어떤 PNG 이미지든 사용 가능합니다. 게임은 셰이더 기반 조명과 시간대를 지원하며 — 배경이 자동으로 조명됩니다.

> 이미지가 없다면, 임시로 `mods/core/backgrounds/home/kitchen.png` → `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`를 복사하세요.

---

## 5단계: DSL 시나리오

`mods/quickstart_modder/scenarios/my_alko_bar.scenario`：

```scenario
scene: my_alko_bar
priority: 10
---
text "당신은 반쯤 빈 술집에 들어선다."
text "지루한 바텐더가 카운터 뒤에 서 있다."

select "[무엇을 하시겠습니까?]"
option "카운터로 다가간다" -> :bar
option "떠난다" -> :leave

:bar
text "바텐더가 고개를 끄덕인다."
text "— 뭘 드릴까요?"
end

:leave
switch_scene town_entry
end
```

### 여기서 무슨 일이 일어나고 있나

- **`text`** — 대화 줄을 표시합니다. `key=` 매개변수는 로컬라이제이션 키입니다.
- **`select`** — 선택 메뉴를 엽니다. 따옴표 안의 텍스트는 프롬프트입니다.
- **`option`** — 선택 옵션. `-> :label` — 선택 시 이동할 위치.
- **`:bar`**, **`:leave`** — 점프용 레이블.
- **`switch_scene`** — 플레이어를 다른 씬으로 이동시킵니다.
- **`end`** — 시나리오를 종료합니다 (플레이어는 씬에 머무릅니다).

---

## 6단계: 마을 확장 (씬 우선순위)

이제 원본 `core` 파일을 **건드리지 않고** 마을 씬에 "술집 가기" 옵션을 추가합니다.

`mods/quickstart_modder/scenarios/town_entry_override.scenario`：

```scenario
scene: town_entry
priority: 1
---
select "오늘 마을 냄새가 다르군..."
option "집으로 돌아간다" -> :home
option "술집에 간다" -> :alko_bar

:home
switch_scene home_outside
end

:alko_bar
switch_scene my_alko_bar
end
```

### 왜 이것이 작동하는가

`town_entry` 씬에는 이제 두 개의 시나리오가 있습니다:
- `priority: 10` — 기본값 (`core` 모드에서)
- `priority: 1` — 우리 것 (`quickstart_modder`에서)

엔진은 **가장 낮은** `priority`의 시나리오를 실행합니다. 우리의 `priority: 1`이 승리 — 플레이어가 새 옵션을 봅니다.

> 더 알아보기: [씬 우선순위 시스템](site/docs/mods/scenes/).

---

## 7단계: 확인

게임을 시작합니다:

1. 마을에 들어가면 — "술집에 간다" 옵션이 보입니다.
2. 클릭 — 대화와 선택지가 있는 `my_alko_bar` 씬에 있습니다.
3. "떠난다" 선택 — 마을로 돌아갑니다.

---

## 다음은?

| 하고 싶은 것 | 이동 |
|---|---|
| 모든 DSL 명령 마스터 | [DSL 명령 참조](site/docs/mods/scenes/scenarios/dsl/) |
| 상호작용 가능한 객체 추가 | [상호작용 객체](site/docs/mods/interactives/) |
| C# 모드 작성 | [레벨 3 — 프로그래머](#quickstart/programmer) |

---

## 완성된 예제 다운로드

<a href="examples/quickstart_modder.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
quickstart_modder.zip 다운로드
</a>

다운로드 가능한 모드는 영어로 되어 있습니다.
