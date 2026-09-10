# 等级 3 — 程序员：C# 模组

<div class="qs-meta">
<span>C#</span>
<span>20 分钟</span>
<span>Roslyn 编译</span>
</div>

你将编写一个 C# 模组，为游戏添加：自定义 DSL 动作和菜单设置。代码通过 Roslyn 即时编译 — 无需 Godot。

---

## 步骤 1：模组结构

```filestree
mods/
└── quickstart_programmer/
    ├── manifest.json
    ├── src/
    │   ├─── MyModSettings.cs
    │   ├─── MyMod.cs
    │   └─── MyEffectAction.cs
    └── locale/
        └─── zh.properties
```

---

## 步骤 2：manifest.json

`mods/quickstart_programmer/manifest.json`：

```jsonc
{
  "id": "quickstart_programmer",   // 模组 ID
  "name": "My Code Mod",           // 模组列表中显示的模组名称
  "description": "新动作和模组设置",  // 模组列表中显示的模组描述
  "sources": ["src/*.cs"],         // 收集源文件的 glob 模式（包含所有 ".cs" 文件）
  "author": "programmer",          // 作者
  "depends": ["core"]              // 依赖核心游戏（无需手动排列模组加载顺序）
}
```

`sources` 字段指定要通过 Roslyn 编译哪些 C# 文件。

---

## 步骤 3：入口点 — IMod

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

            GameLogger.Log("MY_MOD", "我的模组已加载！", LogLevel.Message);
        }

    }
}
```

`IMod` 接口是任何 C# 模组的必需入口点。`Initialize` 方法在模组加载时调用。

> 如果要处理日志，请使用 `GameLogger.Log(...)` 替代 `GD.Print(...)`。

---

## 步骤 4：菜单设置 — IModSettings

添加到 `MyModSettings.cs`：

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

模组加载后，前往 "设置 → Mod Settings" — 你的模组会显示音量滑块和开关。

---

## 步骤 5：自定义 DSL 动作 — IScenarioAction

让我们添加一个新的 `my_effect` 动作，可在 `.scenario` 文件中使用。创建 `MyEffectAction.cs`：

```csharp
using Cthangover.Core.Scenarios;
using Cthangover.Core.Actions;
using Cthangover.Core.Utils;

public class MyEffectAction : IScenarioAction
{
    public string Name => "my_effect";

    public void Run(IActionContext context)
    {
        GameLogger.Log("MY_MOD", "新动作已执行！", LogLevel.Message);
    }
}
```

现在在任何 `.scenario` 文件中你可以写：

```scenario
action my_effect
```

引擎通过反射找到你的类并调用 `Execute`。

---

## 步骤 6：本地化

`mods/quickstart_programmer/locale/zh.properties`：

```properties
mymod/effect_volume = 特效音量
mymod/enable_mod = 启用模组
```

---

## 步骤 7：检查

启动游戏：

1. 打开设置菜单 → "Mod Settings" 标签页 → 你的模组带有滑块和复选框。
2. 编写一个测试 `.scenario`，使用 `action my_effect` 命令。
3. 游戏日志显示 `[MY_MOD] 新动作已执行！`。

---

## 下一步？

| 想要 | 前往 |
|---|---|
| 订阅场景进入/退出事件 | [场景订阅](site/docs/mods/configs/) |
| 创建自己的战斗模式 | [战斗系统](site/docs/mods/battle/) |
| 制作工具窗口 | [创建自定义工具](site/docs/mods/tools/tutorial) |
| 用 GDScript 编写模组 | [Mod 中的 GDScript](site/docs/mods/src/gdscript/) |
| 所有可用接口 | [Mod 中的 C# 源代码](site/docs/mods/src/) |

---

## 下载完整示例

<a href="examples/quickstart_programmer.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
下载 quickstart_programmer.zip
</a>

可下载的模组为英文版本。
