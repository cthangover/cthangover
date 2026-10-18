# 레벨 3 — 프로그래머: C# 모드

<div class="qs-meta">
<span>C#</span>
<span>20분</span>
<span>Roslyn 컴파일</span>
</div>

게임에 커스텀 DSL 액션과 메뉴 설정을 추가하는 C# 모드를 작성합니다. 코드는 게임 시작 시 Roslyn으로 즉석에서 컴파일됩니다 — Godot도 `dotnet build`도 필요 없습니다.

---

## 1단계: 모드 구조

```filestree
mods/
└── quickstart_programmer/
    ├── manifest.json
    ├── src/
    │   ├── MyModSettings.cs
    │   ├── MyMod.cs
    │   └── MyEffectAction.cs
    ├── scenarios/
    │   └── my_effect_demo.scenario
    └── locale/
        ├── ru.properties
        └── en.properties
```

---

## 2단계: manifest.json

`mods/quickstart_programmer/manifest.json`:

```jsonc
{
  "id": "quickstart_programmer",                 // 모드 ID
  "name": "My Code Mod",                         // 모드 목록에 표시되는 모드 이름
  "description": "새 액션과 모드 설정",             // 모드 목록에 표시되는 모드 설명
  "sources": ["src/*.cs"],                       // 소스를 모으는 패턴 (모든 ".cs" 파일)
  "author": "programmer",                        // 작성자
  "depends": ["cthangover_game"]               // 본편 게임에 대한 의존성 (모드 로드 순서를 직접 정할 필요가 없음)
}
```

`sources` 필드는 Roslyn으로 컴파일할 C# 파일을 지정합니다. 컴파일 오류(파일과 줄 번호 포함)는 로그의 `MODS_COMPILE` 카테고리에 기록됩니다.

---

## 3단계: 진입점 — IModInitializer

`mods/quickstart_programmer/src/MyMod.cs`:

```csharp
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.MyCodeMod
{
    public class MyMod : IModInitializer
    {
        public void OnModLoaded(string modId) { }
        
        public void OnModResourcesReady()
        {
            
            GameLogger.Log("MY_MOD", "My mod loaded!", LogLevel.Message);
        }
        
    }
}
```

`IModInitializer` 인터페이스는 모드 C# 코드의 수명 주기 진입점이며, 구현은 리플렉션으로 발견됩니다. `OnModLoaded`는 각 모드가 로드될 때 호출되고(내 모드만이 아닙니다 — `modId`에 로드된 모드의 id가 들어옴), `OnModResourcesReady`는 모든 모드와 리소스가 로드된 후 한 번 호출됩니다.

> 로그를 다루려면 `GD.Print(...)` 대신 `GameLogger.Log(...)`를 사용하세요.

---

## 4단계: 메뉴 설정 — IModSettings

`mods/quickstart_programmer/src/MyModSettings.cs`:

```csharp
using System.Collections.Generic;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;

public class MyModSettings : IModSettings
{
    private float _volume = 1.0f;
    private bool _enabled = true;

    public IReadOnlyList<ModSettingDefinition> GetDefinitions()
    {
        return new List<ModSettingDefinition>
        {
            new()
            {
                Key = "effect_volume",
                Name = "mymod/effect_volume",
                Type = SettingType.Slider,
                DefaultValue = "1.0",
                Min = 0f,
                Max = 2f,
                Step = 0.1f
            },
            new()
            {
                Key = "enable_mod",
                Name = "mymod/enable_mod",
                Type = SettingType.Bool,
                DefaultValue = "true"
            }
        };
    }

    public void WriteValues(DataBlob blob)
    {
        blob.SetFloat("effect_volume", _volume);
        blob.SetBool("enable_mod", _enabled);
    }

    public void ReadValues(DataBlob blob)
    {
        _volume = blob.GetFloat("effect_volume", 1.0f);
        _enabled = blob.GetBool("enable_mod", true);
    }
}
```

`GetDefinitions`는 컨트롤을 기술하고, `WriteValues`/`ReadValues`는 값을 대칭적으로 쓰고 읽습니다. 디스크 저장과 UI는 코어가 담당합니다. 모드가 로드되면 "Settings" → "Mod Settings" 탭에 슬라이더와 토글이 있는 내 모드가 나타납니다.

---

## 5단계: 커스텀 DSL 액션 — IScenarioAction

`.scenario` 파일에서 사용할 수 있는 새 액션 `my_effect`를 추가합니다. `mods/quickstart_programmer/src/MyEffectAction.cs`:

```csharp
using Cthangover.Core.Actions;
using Cthangover.Core.Utils;

public class MyEffectAction : IScenarioAction
{
    public string Name => "my_effect";
    
    public void Run(IActionContext context)
    {
        GameLogger.Log("MY_MOD", "New action executed!", LogLevel.Message);
    }
}
```

이제 어떤 `.scenario` 파일에서든 다음과 같이 쓸 수 있습니다:

```scenario
action my_effect
```

엔진이 리플렉션으로 클래스를 찾아 `Run`을 호출합니다. `context`를 통해 대화 변수(`GetParam`)와 게임 서비스 — 퀘스트, 캐릭터, 조명, 씬 — 에 접근할 수 있습니다.

---

## 6단계: 시험용 시나리오

액션이 동작하는 것을 보기 위해, 플레이어가 집 밖으로 나올 때 호출합니다. `mods/quickstart_programmer/scenarios/my_effect_demo.scenario`:

```scenario
scene: home_outside
priority: 0
---
action my_effect
end
```

`priority: 0` — 우리 시나리오는 기본 시나리오(`priority: 10`)보다 먼저 실행되고, `end` 후에는 게임이 평소대로 계속됩니다: 씬이 원래 메뉴를 엽니다.

---

## 7단계: 로컬라이제이션

설정 라벨은 `Name`의 로컬라이제이션 키입니다. `mods/quickstart_programmer/locale/ru.properties`:

```properties
mymod/effect_volume = Громкость эффекта
mymod/enable_mod = Включить мод
```

`mods/quickstart_programmer/locale/en.properties`:

```properties
mymod/effect_volume = Effect volume
mymod/enable_mod = Enable mod
```

---

## 8단계: 모드를 게임에 연결하기

게임은 설치된 모든 모드를 불러오지 않고, 게임 컬렉션에 포함된 모드만 불러옵니다(게임 UI는 영어와 러시아어로 제공됩니다):

1. 메인 메뉴에서 **"Mods & Collections"** → **"Collections"** 탭을 엽니다.
2. **Cthangover** 게임 카드에서 **"Edit scheme"**을 누릅니다.
3. 스킴에서 아무 메커닉(예: **"Story"**)을 선택하고 **"Add mod"**를 누릅니다.
4. 목록에서 자신의 모드(`manifest.json`의 이름)를 찾아 **"Add"**를 누릅니다.
5. **"Save"**를 누르고 확인합니다. 변경 사항은 다음 게임 실행 시 적용됩니다.

> **개발자용.** 소스에서 메뉴 없이: `godot --path . -- --game=cthangover --new --with-mods=quickstart_programmer --scene=home_outside`.

---

## 9단계: 확인

게임을 다시 시작하세요:

1. "Settings" → "Mod Settings" 탭을 열면 슬라이더와 토글이 있는 내 모드가 있습니다.
2. 게임 로그(`user://logs/cthangover.log`, Windows에서는 `%APPDATA%\cthangover\logs\cthangover.log`)에 로드 후 `[MY_MOD] My mod loaded!`가 기록됩니다.
3. 집 밖으로 나가면 로그에 `[MY_MOD] New action executed!`가 나타납니다.

---

## 다음은?

| 하고 싶은 것 | 이동 |
|---|---|
| 씬 입장/퇴장 구독하기 | [Scene Subscriptions](site/docs/mods/configs/) |
| 나만의 전투 모드 만들기 | [Battle System](site/docs/mods/battle/) |
| 도구 창 만들기 | [Creating a Custom Tool](site/docs/mods/editor_tools/tutorial) |
| GDScript로 모드 작성하기 | [GDScript in Mods](site/docs/mods/src/gdscript/) |
| 사용 가능한 모든 인터페이스 | [C# Source Code in Mods](site/docs/mods/src/) |

---

## 완성된 예제 다운로드

<a href="examples/quickstart_programmer.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
quickstart_programmer.zip 다운로드
</a>

다운로드 가능한 모드는 영어로 되어 있습니다. 압축 파일을 `mods/quickstart_programmer/`에 풀거나 `.zip`을 `user://mods/`에 넣은 뒤 모드를 게임에 연결하세요(8단계).
