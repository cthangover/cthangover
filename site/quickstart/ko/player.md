# 레벨 1 — 플레이어: 캐릭터 패치하기

<div class="qs-meta">
<span>텍스트 편집기</span>
<span>5분</span>
<span>코드 불필요</span>
</div>

패치 시스템을 사용하여 Marao 캐릭터를 강화합니다 — 체력과 공격력을 높이고 "기절" 능력을 추가합니다. 코드는 전혀 필요 없고 JSON만 있으면 됩니다.

---

## 1단계: 모드 폴더 만들기

각 모드는 `mods/` 내의 자체 폴더에 위치합니다. 생성:

```filestree
mods/
└── quickstart_player/
    ├── manifest.json
    └── patches/
        └── character.json
```

---

## 2단계: manifest.json

모든 모드에는 매니페스트가 필요합니다. `mods/quickstart_player/manifest.json` 생성:

```jsonc
{
  "id": "quickstart_player",        // 고유 모드 식별자, 영어 사용
  "name": "Marao 강화",             // 모드 관리 메뉴에 표시되는 이름
  "description": "내 첫 모드!",      // 모드 목록과 카탈로그에 표시되는 설명
  "author": "player",               // 모드 작성자 (임의의 문자열)
  "depends": ["visual_novel_core"]  // 코어 게임 의존성 (수동으로 모드 로드 순서를 정할 필요 없음)
}
```

manifest.json 파일 자체는 필수이며(없으면 모드가 건너뛰어짐), 개별 필드는 엄격하게 필수가 아닙니다. `id`를 생략하면 폴더 이름이 모드 ID로 사용됩니다. 그래도 `id`와 `name`을 채워 넣는 것이 좋습니다. 커널이 폴더를 자동으로 감지하여 모드로 로드합니다.

---

## 3단계: 캐릭터 패치

`mods/quickstart_player/patches/character.json` 생성:

```jsonc
{
  "Items": [
    {
      "Id": "Marao",  // 패치할 캐릭터 ID (visual_novel_core 모드에서 정의됨)

      "Health": 5000, // 새 속성 값 — 원본을 덮어씀
      "Attack": 1200,
      "Point": 2,

      "Actions$add": ["physics/stun"] // $add는 배열에 요소를 추가
    }
  ]
}
```

### 작동 원리

- **`"Id": "Marao"`** — 패치 대상: 식별자 `Marao`를 가진 캐릭터 (`visual_novel_core` 모드에서 정의).
- **`"Health": 5000`**, **`"Attack": 1200`**, **`"Point": 2`** — 캐릭터 필드를 덮어씁니다. 새 값이 이전 값을 대체합니다.
- **`"Actions$add": ["physics/stun"]`** — 접미사 `$add`는 "배열에 추가"를 의미합니다. 액션 `physics/stun`이 목록에 추가됩니다. 기본 `physics/attack`과 `physics/defence`는 그대로 유지됩니다.

> 패치에 대해 더 알아보기: [Patches](site/docs/mods/patches/) 및 [배열 연산](site/docs/mods/patches/arrays-and-identity).

---

## 4단계: 실행 및 확인

게임을 시작합니다. 아무 전투에 입장하면 — Marao가 이제:

- **5000 HP** (16 대신)
- **1200 ATK** (5 대신)
- **2 AP** (1 대신)
- 세 번째 액션 **"기절"** 이 "공격" 및 "방어"와 함께 표시

---

## 다음은?

| 하고 싶은 것 | 이동 |
|---|---|
| 대화가 있는 자체 씬 만들기 | [레벨 2 — 모더](#quickstart/modder) |
| 패치 시스템 깊이 이해하기 | [Patches](site/docs/mods/patches/) |
| 캐릭터에 대해 배우기 | [Characters](site/docs/mods/characters/) |
| 첫 C# 모드 작성하기 | [레벨 3 — 프로그래머](#quickstart/programmer) |

---

## 완성된 예제 다운로드

<a href="examples/quickstart_player.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
quickstart_player.zip 다운로드
</a>

다운로드 가능한 모드는 영어로 되어 있습니다. 설치하려면 모드를 게임의 `mods/` 폴더에 압축 해제하거나, 게임 내 모드 관리자를 사용하세요. 다운로드한 모드는 `user://mods/`에 설치되며, 같은 ID의 내장 모드보다 우선합니다.
