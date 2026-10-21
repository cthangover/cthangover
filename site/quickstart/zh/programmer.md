# 等级 3 — 程序员：C# 模组

<div class="qs-meta">
<span>C#</span>
<span>20 分钟</span>
<span>Roslyn 编译</span>
</div>

你将编写一个 C# 模组，为游戏添加自定义 DSL 动作和菜单设置。代码在游戏启动时通过 Roslyn 即时编译 — 不需要 Godot，也不需要 `dotnet build`。

---

## 步骤 1：模组结构

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

## 步骤 2：manifest.json

`mods/quickstart_programmer/manifest.json`：

```jsonc
{
  "id": "quickstart_programmer",          // 模组 ID
  "name": "My Code Mod",                  // 模组列表中显示的名称
  "description": "新动作和模组设置",         // 模组列表中显示的描述
  "sources": ["src/*.cs"],                // 收集源文件的模式（所有 ".cs" 文件）
  "author": "programmer",                 // 作者
  "depends": ["cthangover_game"]        // 依赖主游戏的内容（无需手动排列模组加载顺序）
}
```

`sources` 字段指定需要通过 Roslyn 编译的 C# 文件。编译错误（带文件和行号）会写入日志的 `MODS_COMPILE` 类别。

---

## 步骤 3：入口点 — IModInitializer

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
            
            GameLogger.Log("MY_MOD", "My mod loaded!", LogLevel.Message);
        }
        
    }
}
```

`IModInitializer` 接口是模组 C# 代码生命周期的入口点；实现类通过反射发现。`OnModLoaded` 在每个模组加载时调用（不仅是你的模组 — `modId` 中是刚加载的模组 id），`OnModResourcesReady` 在所有模组及其资源加载完成后调用一次。

> 如果需要处理日志，请使用 `GameLogger.Log(...)` 而不是 `GD.Print(...)`。

---

## 步骤 4：菜单设置 — IModSettings

`mods/quickstart_programmer/src/MyModSettings.cs`：

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

`GetDefinitions` 描述控件，`WriteValues`/`ReadValues` 对称地写入和读取值；保存到磁盘和界面由核心负责。模组加载后，你的模组会出现在"Settings" → "Mod Settings"标签页中，带有滑块和开关。

---

## 步骤 5：自定义 DSL 动作 — IScenarioAction

添加一个新动作 `my_effect`，可在 `.scenario` 文件中使用。`mods/quickstart_programmer/src/MyEffectAction.cs`：

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

现在你可以在任何 `.scenario` 文件中写：

```scenario
action my_effect
```

引擎会通过反射找到你的类并调用 `Run`。通过 `context` 可以访问对话变量（`GetParam`）和游戏服务：任务、角色、光照、场景。

---

## 步骤 6：用于测试的剧本

为了看到动作的效果，我们在玩家走出家门时调用它。`mods/quickstart_programmer/scenarios/my_effect_demo.scenario`：

```scenario
scene: home_outside
priority: 0
---
action my_effect
end
```

`priority: 0` — 我们的剧本在标准剧本（`priority: 10`）之前运行，`end` 之后游戏照常继续：场景打开它的常规菜单。

---

## 步骤 7：本地化

设置的标签就是 `Name` 中的本地化键。`mods/quickstart_programmer/locale/ru.properties`：

```properties
mymod/effect_volume = Громкость эффекта
mymod/enable_mod = Включить мод
```

`mods/quickstart_programmer/locale/en.properties`：

```properties
mymod/effect_volume = Effect volume
mymod/enable_mod = Enable mod
```

---

## 步骤 8：将模组连接到游戏

游戏不会加载所有已安装的模组，只加载属于游戏合集的模组（游戏界面提供英语和俄语）：

1. 在主菜单中打开 **"Mods & Collections"** → **"Collections"** 标签页。
2. 在 **Cthangover** 游戏卡片上点击 **"Edit scheme"**。
3. 在方案图上选中任意机制（例如 **"Story"**），然后点击 **"Add mod"**。
4. 在列表中找到你的模组（按 `manifest.json` 中的名称），点击 **"Add"**。
5. 点击 **"Save"** 并确认。更改将在下次启动游戏时生效。

> **开发者提示。** 从源码运行、无需菜单：`godot --path . -- --game=cthangover --new --with-mods=quickstart_programmer --scene=home_outside`。

---

## 步骤 9：检查

重启游戏：

1. 打开"Settings" → "Mod Settings"标签页 → 你的模组带有滑块和开关。
2. 游戏日志（`user://logs/cthangover.log`，Windows 上为 `%APPDATA%\cthangover\logs\cthangover.log`）在加载后包含 `[MY_MOD] My mod loaded!`。
3. 走出家门 — 日志中出现 `[MY_MOD] New action executed!`。

---

## 下一步？

| 想要 | 前往 |
|---|---|
| 订阅场景进入/退出 | [Scene Subscriptions](site/docs/mods/configs/) |
| 创建自己的战斗模式 | [Battle System](site/docs/mods/battle/) |
| 制作工具窗口 | [Creating a Custom Tool](site/docs/mods/editor_tools/tutorial) |
| 用 GDScript 编写模组 | [GDScript in Mods](site/docs/mods/src/gdscript/) |
| 所有可用接口 | [C# Source Code in Mods](site/docs/mods/src/) |

---

## 下载完整示例

<a href="examples/quickstart_programmer.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
下载 quickstart_programmer.zip
</a>

可下载的模组为英文版本。将压缩包解压到 `mods/quickstart_programmer/`，或把 `.zip` 放进 `user://mods/`，然后将模组连接到游戏（步骤 8）。
