# 레벨 2 — 모더: DSL 씬

<div class="qs-meta">
<span>JSON + DSL</span>
<span>15분</span>
<span>코드 불필요</span>
</div>

대화, 선택지, 전환이 있는 나만의 씬 — 술집 — 을 담은 모드를 만듭니다. 그리고 원본 파일을 건드리지 않고 기존 마을 씬에 술집 입구를 추가합니다.

---

## 1단계: 모드 구조

폴더와 기본 파일을 만드세요:

```filestree
mods/
└── quickstart_modder/
    ├── manifest.json
    ├── scenes/
    │   └── my_alko_bar.json              # 새로운 장소 — 술집!
    ├── scenarios/
    │   ├── my_alko_bar.scenario          # 술집 안의 시나리오, 플레이어가 새 장소와 상호작용하게 함
    │   └── town_entry_override.scenario  # 마을 씬을 확장해 새 장소로 가는 전환을 추가
    ├── backgrounds/
    │   └── my_alko_bar/
    │       └── bg.png                    # 술집 내부 이미지
    └── sounds/
        └── locations/
            └── alkobar/
                └── bar_ambient.ogg       # 술집 배경 소음
```

---

## 2단계: manifest.json

`mods/quickstart_modder/manifest.json`:

```jsonc
{
  "id": "quickstart_modder",                  // 모드 ID
  "name": "나의 술집",                          // 모드 메뉴에 표시되는 모드 이름
  "description": "마을에 술집 씬을 추가합니다",   // 모드 메뉴에 표시되는 모드 설명
  "author": "modder",                         // 모드 작성자
  "depends": ["cthangover_game"]            // 본편 게임에 대한 의존성 (모드 로드 순서를 직접 정할 필요가 없음)
}
```

---

## 3단계: 씬 정의

`mods/quickstart_modder/scenes/my_alko_bar.json`:

```jsonc
{
  "name": "my_alko_bar",                              // 새롭고 고유한 씬 식별자 (switch_scene에서 사용), 이것이 새 씬이 됨
  "defaultBackground": "my_alko_bar/bg",              // backgrounds/ 안의 PNG 배경 경로 (확장자 없이)
  "defaultAmbient": "locations/alkobar/bar_ambient",  // 이 장소의 환경음 ID (sounds/ 안의 경로, 확장자 없이), 예: 입장 시 재생되는 "술집 소음"
  "defaultScenario": "scenarios/my_alko_bar.scenario" // 다른 것이 없을 때 이 씬에서 실행되는 기본 시나리오
}
```

| 필드 | 설명 |
|---|---|
| `name` | 씬의 고유 ID. `switch_scene`에서 사용 |
| `defaultBackground` | `backgrounds/` 안의 배경 경로 (확장자 없이) |
| `defaultAmbient` | 환경음: `sounds/` 안의 경로, 확장자 없이. 빈 문자열 — 소리 없음 |
| `defaultScenario` | 입장 시 실행되는 `.scenario` 파일 경로 |

---

## 4단계: 배경과 소리

이미지를 `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`에, 환경음을 `mods/quickstart_modder/sounds/locations/alkobar/bar_ambient.ogg`에 넣으세요.

어떤 PNG 이미지든 괜찮습니다. 게임은 셰이더 조명과 시간대를 지원하므로 배경은 자동으로 조명됩니다.

> 이미지가 없다면 임시로 `mods/cthangover_game/backgrounds/home/kitchen.png` → `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`로 복사하세요. 소리 없이도 됩니다: `"defaultAmbient": ""`로 지정하세요.

---

## 5단계: DSL 시나리오

`mods/quickstart_modder/scenarios/my_alko_bar.scenario`:

```scenario
scene: my_alko_bar
priority: 10
---
text "반쯤 빈 술집에 들어선다."
text "카운터 뒤에서 바텐더가 따분해하고 있다."

:menu
select "[무엇을 할까?]"
option "카운터로 간다" -> :bar
option "나간다" -> :leave

:bar
text "바텐더가 고개를 끄덕인다."
text "— 무엇을 드릴까요?"
goto -> :menu

:leave
switch_scene town_entry
end
```

### 여기서 무슨 일이 일어나고 있나

- **`scene:`** / **`priority:`** — 헤더: 시나리오가 어느 씬에 속하고 어떤 순서로 실행되는지. 헤더와 본문은 `---` 줄로 구분합니다.
- **`text`** — 대화 한 줄을 표시합니다. 매개변수 `key=`는 로컬라이제이션 키입니다(여기서는 필요 없음: 없으면 텍스트 자체가 표시됨).
- **`select`** — 선택 메뉴를 엽니다. 따옴표 안의 텍스트는 프롬프트입니다.
- **`option`** — 선택지입니다. `-> :label` — 선택 시 이동할 곳.
- **`:menu`**, **`:bar`**, **`:leave`** — 이동용 레이블.
- **`goto -> :menu`** — 무조건 이동: 바텐더와 이야기한 뒤 메뉴가 다시 열립니다.
- **`switch_scene`** — 플레이어를 다른 씬으로 이동시킵니다.
- **`end`** — 시나리오를 끝냅니다.

---

## 6단계: 마을 확장 (씬 우선순위)

이제 `cthangover_game`의 원본 파일을 **건드리지 않고** 마을 씬에 술집 입구를 추가합니다.

`mods/quickstart_modder/scenarios/town_entry_override.scenario`:

```scenario
scene: town_entry
priority: 1
---
select "반쯤 열린 술집 문틈으로 음악이 흘러나온다..."
option "술집에 들어간다" -> :alko_bar
option "지나친다" -> :pass

:alko_bar
switch_scene my_alko_bar
end

:pass
end
```

### 왜 이것이 작동하는가

이제 `town_entry` 씬에는 여러 시나리오가 있습니다:

| priority | 시나리오 | 모드 |
|---|---|---|
| `1` | 우리의 `town_entry_override` | `quickstart_modder` |
| `10`–`30` | 무라카미와의 만남 (해당 `condition:`이 충족되는 동안만) | `cthangover_murakami` |
| `40` | 기본 마을 메뉴 | `cthangover_game` |

씬에 들어가면 엔진은 `priority` 오름차순 — **가장 작은** 값부터 — 으로 조건을 만족하는 첫 시나리오를 실행합니다. 시나리오가 씬을 바꾸지 않고 끝나면 다음 시나리오가 시작됩니다. 그래서:

- "술집에 들어간다" — `switch_scene`이 플레이어를 술집으로 데려갑니다;
- "지나친다" — 우리 시나리오는 그냥 끝나고(`end`), 이후는 원래 게임대로 진행됩니다: 무라카미의 스토리 씬 또는 기본 마을 메뉴.

이렇게 모드는 아무것도 망가뜨리지 않고 새로운 것을 추가합니다. 우리 시나리오가 마을 메뉴를 직접 그렸다면 스토리와 다른 모드를 모두 가렸을 것입니다.

> 더 알아보기: [Scene Priority System](site/docs/mods/scenes/).

---

## 7단계: 모드를 게임에 연결하기

게임은 설치된 모든 모드를 불러오지 않고, 게임 컬렉션에 포함된 모드만 불러옵니다. 모드를 추가하세요(게임 UI는 영어와 러시아어로 제공됩니다):

1. 메인 메뉴에서 **"Mods & Collections"** → **"Collections"** 탭을 엽니다.
2. **Cthangover** 게임 카드에서 **"Edit scheme"**을 누릅니다.
3. 스킴에서 아무 메커닉(예: **"Story"**)을 선택하고 **"Add mod"**를 누릅니다.
4. 목록에서 자신의 모드(`manifest.json`의 이름)를 찾아 **"Add"**를 누릅니다.
5. **"Save"**를 누르고 확인합니다. 변경 사항은 다음 게임 실행 시 적용됩니다.

---

## 8단계: 확인

게임을 다시 시작하세요:

1. 마을에 들어가면 가장 먼저 "술집에 들어간다" / "지나친다" 선택지가 나타납니다.
2. "술집에 들어간다"를 누르면 — 배경, 술집 소음, 대화, 선택지가 있는 나만의 `my_alko_bar` 씬에 있습니다.
3. "나간다"를 선택하면 — 마을로 돌아갑니다.
4. "지나친다"를 선택하면 — 게임이 평소대로 계속됩니다.

> **개발자용.** 소스에서는 프롤로그를 플레이하지 않고 바로 씬을 열 수 있습니다: `godot --path . -- --game=cthangover --new --with-mods=quickstart_modder --scene=town_entry`.

---

## 다음은?

| 하고 싶은 것 | 이동 |
|---|---|
| 모든 DSL 명령어 익히기 | [DSL Command Reference](site/docs/mods/scenes/scenarios/dsl/) |
| 인터랙티브 오브젝트 추가하기 | [Interactive Objects](site/docs/mods/interactives/) |
| C# 모드 작성하기 | [레벨 3 — 프로그래머](#quickstart/programmer) |

---

## 완성된 예제 다운로드

<a href="examples/quickstart_modder.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
quickstart_modder.zip 다운로드
</a>

다운로드 가능한 모드의 대사는 러시아어로 되어 있습니다. 압축 파일을 `mods/quickstart_modder/`에 풀거나 `.zip`을 `user://mods/`에 넣은 뒤 모드를 게임에 연결하세요(7단계).
