# 레벨 3 — 프로그래머: C# 모드

<div class="qs-meta">
<span>C#</span>
<span>20분</span>
<span>Roslyn 컴파일</span>
</div>

게임에 커스텀 DSL 액션과 메뉴 설정을 추가하는 C# 모드를 작성합니다. 코드는 Roslyn을 통해 실시간 컴파일됩니다 — Godot는 필요 없습니다.

---

## 1단계: 모드 구조

```filestree
mods/
└── quickstart_programmer/
    ├── manifest.json
    ├── src/
    │   ├─── MyModSettings.cs
    │   ├─── MyMod.cs
    │   └─── MyEffectAction.cs
    └── locale/
        └─── ko.properties
```

---

## 2단계: manifest.json

`mods/quickstart_programmer/manifest.json`：

```jsonc
{
  "id": "quickstart_programmer",      // 모드 ID
  "name": "My Code Mod",              // 모드 목록에 표시되는 모드 이름
  "description": "새 액션과 모드 설정",  // 모드 목록에 표시되는 모드 설명
  "sources": ["src/*.cs"],            // 소스 파일 수집을 위한 glob 패턴 (모든 ".cs" 파일 포함)
  "author": "programmer",             // 작성자
  "depends": ["core"]                 // 코어 게임 의존성 (수동으로 모드 로드 순서를 정할 필요 없음)
}
```

`sources` 필드는 Roslyn으로 컴파일할 C# 파일을 지정합니다.

---

## 3단계: 진입점 — IMod

`mods/quickstart_programmer/src/MyMod.cs`：

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

            GameLogger.Log("MY_MOD", "내 모드가 로드되었습니다!", LogLevel.Message);
        }

    }
}
```

`IMod` 인터페이스는 모든 C# 모드의 필수 진입점입니다. `Initialize` 메서드는 모드가 로드될 때 호출됩니다.

> 로그 작업을 하려면 `GD.Print(...)` 대신 `GameLogger.Log(...)`를 사용하세요.

---

## 4단계: 메뉴 설정 — IModSettings

`MyModSettings.cs`에 추가：

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

모드 로드 후 "설정 → Mod Settings"로 이동하면 — 볼륨 슬라이더와 토글이 표시됩니다.

---

## 5단계: 커스텀 DSL 액션 — IScenarioAction

`.scenario` 파일에서 사용할 수 있는 새로운 `my_effect` 액션을 추가해 보겠습니다. `MyEffectAction.cs` 생성：

```csharp
using Cthangover.Core.Scenarios;
using Cthangover.Core.Actions;
using Cthangover.Core.Utils;

public class MyEffectAction : IScenarioAction
{
    public string Name => "my_effect";

    public void Run(IActionContext context)
    {
        GameLogger.Log("MY_MOD", "새 액션이 실행되었습니다!", LogLevel.Message);
    }
}
```

이제 어떤 `.scenario` 파일에서든 다음과 같이 작성할 수 있습니다：

```scenario
action my_effect
```

엔진이 리플렉션을 통해 클래스를 찾아 `Execute`를 호출합니다.

---

## 6단계: 로컬라이제이션

`mods/quickstart_programmer/locale/ko.properties`：

```properties
mymod/effect_volume = 이펙트 볼륨
mymod/enable_mod = 모드 활성화
```

---

## 7단계: 확인

게임을 시작합니다：

1. 설정 메뉴 열기 → "Mod Settings" 탭 → 슬라이더와 체크박스가 있는 모드가 표시됩니다.
2. `action my_effect` 명령을 포함한 테스트 `.scenario`를 작성합니다.
3. 게임 로그에 `[MY_MOD] 새 액션이 실행되었습니다!`가 표시됩니다.

---

## 다음은？

| 하고 싶은 것 | 이동 |
|---|---|
| 씬 진입/퇴장 구독하기 | [씬 구독](site/docs/mods/configs/) |
| 자체 전투 모드 만들기 | [전투 시스템](site/docs/mods/battle/) |
| 도구 창 만들기 | [커스텀 도구 만들기](site/docs/mods/tools/tutorial) |
| GDScript로 모드 작성하기 | [모드에서의 GDScript](site/docs/mods/src/gdscript/) |
| 사용 가능한 모든 인터페이스 | [모드에서의 C# 소스 코드](site/docs/mods/src/) |

---

## 완성된 예제 다운로드

<a href="examples/quickstart_programmer.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
quickstart_programmer.zip 다운로드
</a>

다운로드 가능한 모드는 영어로 되어 있습니다.
