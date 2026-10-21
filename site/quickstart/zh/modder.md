# 等级 2 — 模组制作者：DSL 场景

<div class="qs-meta">
<span>JSON + DSL</span>
<span>15 分钟</span>
<span>无需代码</span>
</div>

你将创建一个带有自定义场景的模组 — 一间有对话、选项和场景切换的酒吧。同时，你还会在不修改原始文件的情况下，为现有的城镇场景添加酒吧入口。

---

## 步骤 1：模组结构

创建文件夹和基础文件：

```filestree
mods/
└── quickstart_modder/
    ├── manifest.json
    ├── scenes/
    │   └── my_alko_bar.json              # 我们的新地点 — 酒馆！
    ├── scenarios/
    │   ├── my_alko_bar.scenario          # 酒吧内的剧本，让玩家与新地点互动
    │   └── town_entry_override.scenario  # 扩展城镇场景，添加通往新地点的入口
    ├── backgrounds/
    │   └── my_alko_bar/
    │       └── bg.png                    # 酒吧内景图片
    └── sounds/
        └── locations/
            └── alkobar/
                └── bar_ambient.ogg       # 酒吧背景嘈杂声
```

---

## 步骤 2：manifest.json

`mods/quickstart_modder/manifest.json`：

```jsonc
{
  "id": "quickstart_modder",              // 模组 ID
  "name": "我的酒吧",                      // 模组菜单中显示的名称
  "description": "为城镇添加酒吧场景",       // 模组菜单中显示的描述
  "author": "modder",                     // 模组作者
  "depends": ["cthangover_game"]        // 依赖主游戏的内容（无需手动排列模组加载顺序）
}
```

---

## 步骤 3：场景定义

`mods/quickstart_modder/scenes/my_alko_bar.json`：

```jsonc
{
  "name": "my_alko_bar",                              // 新的唯一场景标识符（用于 switch_scene），这就是我们的新场景
  "defaultBackground": "my_alko_bar/bg",              // backgrounds/ 中 PNG 背景的路径（不含扩展名）
  "defaultAmbient": "locations/alkobar/bar_ambient",  // 该地点的环境音 ID（sounds/ 中的路径，不含扩展名），例如进入时播放的"酒吧嘈杂声"
  "defaultScenario": "scenarios/my_alko_bar.scenario" // 没有其他剧本时在此场景运行的默认剧本
}
```

| 字段 | 说明 |
|---|---|
| `name` | 场景的唯一 ID。用于 `switch_scene` |
| `defaultBackground` | `backgrounds/` 中背景的路径（不含扩展名） |
| `defaultAmbient` | 环境音：`sounds/` 中的路径，不含扩展名。空字符串 — 无声音 |
| `defaultScenario` | 进入场景时运行的 `.scenario` 文件路径 |

---

## 步骤 4：背景与声音

将图片放到 `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`，将环境音放到 `mods/quickstart_modder/sounds/locations/alkobar/bar_ambient.ogg`。

任何 PNG 图片都可以。游戏支持着色器光照和昼夜时间 — 背景会自动被照亮。

> 如果没有自己的图片，可以临时复制 `mods/cthangover_game/backgrounds/home/kitchen.png` → `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`。也可以不要声音：设置 `"defaultAmbient": ""`。

---

## 步骤 5：DSL 剧本

`mods/quickstart_modder/scenarios/my_alko_bar.scenario`：

```scenario
scene: my_alko_bar
priority: 10
---
text "你走进一间半空的酒吧。"
text "吧台后，酒保正百无聊赖。"

:menu
select "[你要做什么？]"
option "走到吧台" -> :bar
option "离开" -> :leave

:bar
text "酒保向你点头。"
text "— 想喝点什么？"
goto -> :menu

:leave
switch_scene town_entry
end
```

### 这里发生了什么

- **`scene:`** / **`priority:`** — 头部：剧本属于哪个场景、按什么顺序运行。头部与正文用 `---` 行分隔。
- **`text`** — 显示一行对话。参数 `key=` 是本地化键（这里不需要：没有时直接显示文本本身）。
- **`select`** — 打开选项菜单。引号中的文本是提示语。
- **`option`** — 一个选项。`-> :label` — 选择后跳转的位置。
- **`:menu`**、**`:bar`**、**`:leave`** — 跳转标签。
- **`goto -> :menu`** — 无条件跳转：与酒保交谈后，菜单会再次打开。
- **`switch_scene`** — 将玩家带到另一个场景。
- **`end`** — 结束剧本。

---

## 步骤 6：扩展城镇（场景优先级）

现在，在**不修改** `cthangover_game` 原始文件的情况下，为城镇场景添加酒吧入口。

`mods/quickstart_modder/scenarios/town_entry_override.scenario`：

```scenario
scene: town_entry
priority: 1
---
select "半掩的酒吧门里传出音乐声……"
option "走进酒吧" -> :alko_bar
option "路过" -> :pass

:alko_bar
switch_scene my_alko_bar
end

:pass
end
```

### 为什么有效

`town_entry` 场景现在有多个剧本：

| priority | 剧本 | 模组 |
|---|---|---|
| `1` | 我们的 `town_entry_override` | `quickstart_modder` |
| `10`–`30` | 与村上相遇（仅在其 `condition:` 满足时） | `cthangover_murakami` |
| `40` | 标准城镇菜单 | `cthangover_game` |

进入场景时，引擎按 `priority` 升序 — 从**最小**值开始 — 运行第一个符合条件的剧本。当一个剧本在没有切换场景的情况下结束时，下一个剧本就会开始。因此：

- "走进酒吧" — `switch_scene` 把玩家带进酒吧；
- "路过" — 我们的剧本直接结束（`end`），之后一切如原版游戏：村上的剧情场景或标准城镇菜单。

这样，模组在不破坏任何东西的前提下添加了新内容。如果我们的剧本自己绘制城镇菜单，就会同时盖住剧情和其他模组。

> 了解更多：[Scene Priority System](site/docs/mods/scenes/)。

---

## 步骤 7：将模组连接到游戏

游戏不会加载所有已安装的模组，只加载属于游戏合集的模组。把你的模组添加进去（游戏界面提供英语和俄语）：

1. 在主菜单中打开 **"Mods & Collections"** → **"Collections"** 标签页。
2. 在 **Cthangover** 游戏卡片上点击 **"Edit scheme"**。
3. 在方案图上选中任意机制（例如 **"Story"**），然后点击 **"Add mod"**。
4. 在列表中找到你的模组（按 `manifest.json` 中的名称），点击 **"Add"**。
5. 点击 **"Save"** 并确认。更改将在下次启动游戏时生效。

---

## 步骤 8：检查

重启游戏：

1. 进入城镇 — 首先出现"走进酒吧" / "路过"的选择。
2. 点击"走进酒吧" — 你来到自己的 `my_alko_bar` 场景，有背景、酒吧嘈杂声、对话和选项。
3. 选择"离开" — 回到城镇。
4. 选择"路过" — 游戏照常继续。

> **开发者提示。** 从源码可以直接打开该场景，无需玩完序章：`godot --path . -- --game=cthangover --new --with-mods=quickstart_modder --scene=town_entry`。

---

## 下一步？

| 想要 | 前往 |
|---|---|
| 掌握所有 DSL 命令 | [DSL Command Reference](site/docs/mods/scenes/scenarios/dsl/) |
| 添加交互对象 | [Interactive Objects](site/docs/mods/interactives/) |
| 编写 C# 模组 | [等级 3 — 程序员](#quickstart/programmer) |

---

## 下载完整示例

<a href="examples/quickstart_modder.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
下载 quickstart_modder.zip
</a>

可下载的模组的对话文本为俄语。将压缩包解压到 `mods/quickstart_modder/`，或把 `.zip` 放进 `user://mods/`，然后将模组连接到游戏（步骤 7）。
