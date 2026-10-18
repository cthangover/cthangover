# 레벨 1 — 플레이어: 캐릭터 패치하기

<div class="qs-meta">
<span>텍스트 편집기</span>
<span>5분</span>
<span>코드 불필요</span>
</div>

패치 시스템을 사용해 캐릭터 Marao를 강화합니다 — 체력과 공격력을 올리고 "기절" 능력을 추가합니다. 코드는 한 줄도 필요 없고 JSON만 있으면 됩니다.

---

## 1단계: 모드 폴더 만들기

모든 모드는 `mods/`(게임 옆) 또는 `user://mods/`(사용자 데이터 폴더, Windows에서는 `%APPDATA%\cthangover\mods`) 안의 자기 폴더에 있습니다. 다음을 만드세요:

```filestree
mods/
└── quickstart_player/
    ├── manifest.json
    └── patches/
        └── character.json
```

---

## 2단계: manifest.json

모든 모드에는 매니페스트가 필요합니다. `mods/quickstart_player/manifest.json`을 만드세요:

```jsonc
{
  "id": "quickstart_player",        // 모드의 고유 식별자, 영어로 작성
  "name": "Marao Buff",             // 모드 관리 메뉴에 표시되는 이름
  "description": "나의 첫 모드!",    // 모드 목록과 카탈로그에 표시되는 설명
  "author": "player",               // 모드 작성자 (임의의 문자열)
  "depends": ["cthangover_game"]  // 본편 게임에 대한 의존성 (모드 로드 순서를 직접 정할 필요가 없음)
}
```

매니페스트 파일 자체는 필수입니다 — 없으면 모드를 건너뜁니다 — 하지만 반드시 필요한 필드는 없습니다: `id`를 생략하면 폴더 이름이 모드 식별자가 됩니다. 그래도 `id`와 `name`은 채우는 것을 권장합니다. 게임은 모드 폴더를 스스로 찾지만, 모드는 연결한 뒤에야 게임에 들어갑니다(4단계).

---

## 3단계: 캐릭터 패치

`mods/quickstart_player/patches/character.json`을 만드세요:

```jsonc
{
  "Items": [
    {
      "Id": "Marao", // 패치할 캐릭터 ID (본편 모드에 정의됨)

      "Health": 5000, // 새 속성 값 — 원래 값을 덮어씀
      "Attack": 1200,
      "Point": 2,

      "Actions$add": ["physics/stun"] // $add는 배열에 요소를 추가함
    }
  ]
}
```

### 작동 원리

- **`"Id": "Marao"`** — 패치 대상: 식별자가 `Marao`인 캐릭터(`cthangover_game` 모드에 정의됨).
- **`"Health": 5000`**, **`"Attack": 1200`**, **`"Point": 2`** — 캐릭터의 필드를 덮어씁니다. 새 값이 이전 값을 대체합니다.
- **`"Actions$add": ["physics/stun"]`** — 접미사 `$add`는 "배열에 요소 추가"를 뜻합니다. 액션 `physics/stun`이 목록에 추가되고, 기본 `physics/attack`과 `physics/defence`는 그대로 남습니다. 기절은 2 AP가 필요하므로 `Point`도 올렸습니다.

> 패치에 대해 더 알아보기: [Patches](site/docs/mods/patches/) 및 [Array Operations](site/docs/mods/patches/arrays-and-identity).

---

## 4단계: 모드를 게임에 연결하기

게임은 설치된 모든 모드를 불러오지 않고, 게임 컬렉션에 포함된 모드만 불러옵니다. 새 모드를 한 번 추가하세요(게임 UI는 영어와 러시아어로 제공됩니다):

1. 메인 메뉴에서 **"Mods & Collections"** → **"Collections"** 탭을 엽니다.
2. **Cthangover** 게임 카드에서 **"Edit scheme"**을 누릅니다.
3. 스킴에서 아무 메커닉(예: **"Story"**)을 선택하고 **"Add mod"**를 누릅니다.
4. 목록에서 자신의 모드(`manifest.json`의 이름)를 찾아 **"Add"**를 누릅니다.
5. **"Save"**를 누르고 확인합니다. 변경 사항은 다음 게임 실행 시 적용됩니다.

> **개발자용.** 소스에서 실행할 때는 메뉴 없이 플래그로 모드를 연결할 수 있습니다: `godot --path . -- --game=cthangover --new --with-mods=quickstart_player`.

---

## 5단계: 실행 및 확인

게임을 다시 시작하고 새 게임을 시작하세요. 아무 전투에나 들어가면 Marao는 이제:

- HP 16 대신 **5000**
- ATK 5 대신 **1200**
- AP 1 대신 **2**
- "Basic attack", "Defence" 옆에 세 번째 액션 **"Stun"**(기절)

---

## 다음은?

| 하고 싶은 것 | 이동 |
|---|---|
| 대화가 있는 나만의 씬 만들기 | [레벨 2 — 모더](#quickstart/modder) |
| 패치 시스템 깊이 이해하기 | [Patches](site/docs/mods/patches/) |
| 캐릭터에 대해 알아보기 | [Characters](site/docs/mods/characters/) |
| 첫 C# 모드 작성하기 | [레벨 3 — 프로그래머](#quickstart/programmer) |

---

## 완성된 예제 다운로드

<a href="examples/quickstart_player.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
quickstart_player.zip 다운로드
</a>

다운로드 가능한 모드는 영어로 되어 있습니다. 설치하려면 압축 파일을 게임의 `mods/quickstart_player/` 폴더에 풀거나 `.zip` 파일 자체를 `user://mods/`에 넣으세요: 게임은 압축 파일에서 바로 모드를 읽고, `user://mods/`의 모드는 같은 id의 내장 모드보다 우선합니다. 설치 후 모드를 게임에 연결하세요(4단계).
