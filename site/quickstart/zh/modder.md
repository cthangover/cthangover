# 等级 2 — 模组制作者：DSL 场景

<div class="qs-meta">
<span>JSON + DSL</span>
<span>15 分钟</span>
<span>无需代码</span>
</div>

你将创建一个包含自己场景的模组 — 一个有对话、选择和跳转的房间。同时你还会在现有厨房场景中添加"去酒馆"选项，而不碰原始文件。

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
    │   ├── my_alko_bar.scenario          # 酒馆内的场景，让玩家与新地点互动
    │   └── town_entry_override.scenario  # 覆盖城镇场景，添加通往新地点的跳转
    ├── backgrounds/
    │   └── my_alko_bar/
    │       └── bg.png                    # 酒馆内部图片
    ├── backgrounds/
    │   └── sounds/
    │       └── alko_bar_ambient.ogg      # 酒馆环境噪音
    └── locale/
        └── ru.properties                 # 俄语本地化
```

---

## 步骤 2：manifest.json

`mods/quickstart_modder/manifest.json`：

```jsonc
{
  "id": "alko_bar",                        // 模组 ID
  "name": "我的酒馆",                       // 模组菜单中显示的模组名称
  "description": "在城镇中添加一个酒馆场景",   // 模组菜单中显示的模组描述
  "author": "modder",                      // 模组作者
  "depends": ["core"]                      // 依赖核心游戏（无需手动排列模组加载顺序）
}
```

---

## 步骤 3：场景定义

`mods/quickstart_modder/scenes/my_alko_bar.json`：

```jsonc
{
  "name": "my_alko_bar",                              // 创建新的唯一场景标识符（在 switch_scene 中使用），这将是新场景。
  "defaultBackground": "my_alko_bar/bg",              // backgrounds/ 中 PNG 背景的路径（无扩展名）
  "defaultAmbient": "locations/alkobar/bar_ambient",  // 此位置环境音的 ID（无扩展名），例如进入场景时播放的"酒馆噪音"
  "defaultScenario": "scenarios/my_alko_bar.scenario" // 如果没有指定其他内容，将在此场景中运行的默认场景
}
```

| 字段 | 描述 |
|---|---|
| `name` | 唯一场景 ID。在 `switch_scene` 中使用 |
| `defaultBackground` | `backgrounds/` 中的背景路径（无扩展名） |
| `defaultAmbient` | 环境音。空字符串 — 无声音 |
| `defaultScenario` | 进入场景时运行的 `.scenario` 文件路径 |

---

## 步骤 4：背景

将图片放在 `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`。

任何 PNG 图片都可以使用。游戏支持基于着色器的光照和昼夜系统 — 背景会自动光照。

> 如果没有自己的图片，临时复制 `mods/core/backgrounds/home/kitchen.png` → `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`。

---

## 步骤 5：DSL 场景

`mods/quickstart_modder/scenarios/my_alko_bar.scenario`：

```scenario
scene: my_alko_bar
priority: 10
---
text "你走进一家半空的酒馆。"
text "一个无聊的酒保站在吧台后面。"

select "[你要做什么？]"
option "走向吧台" -> :bar
option "离开" -> :leave

:bar
text "酒保向你点了点头。"
text "— 来点什么？"
end

:leave
switch_scene town_entry
end
```

### 这里发生了什么

- **`text`** — 显示一行对话。参数 `key=` 是本地化键。
- **`select`** — 打开选择菜单。引号中的文本是提示。
- **`option`** — 选择选项。`-> :label` — 选择后跳转的位置。
- **`:bar`**、**`:leave`** — 跳转标签。
- **`switch_scene`** — 将玩家移动到另一个场景。
- **`end`** — 结束场景（玩家留在场景中）。

---

## 步骤 6：扩展城镇（场景优先级）

现在我们在城镇场景中添加"去酒馆"选项，**不碰**原始的 `core` 文件。

`mods/quickstart_modder/scenarios/town_entry_override.scenario`：

```scenario
scene: town_entry
priority: 1
---
select "今天的城镇闻起来不一样..."
option "回家" -> :home
option "去酒馆" -> :alko_bar

:home
switch_scene home_outside
end

:alko_bar
switch_scene my_alko_bar
end
```

### 为什么有效

`town_entry` 场景现在有两个场景：
- `priority: 10` — 默认（来自 `core` 模组）
- `priority: 1` — 我们的（来自 `quickstart_modder`）

引擎运行 **最低** `priority` 的场景。我们的 `priority: 1` 胜出 — 玩家看到新选项。

> 了解更多：[场景优先级系统](site/docs/mods/scenes/)。

---

## 步骤 7：检查

启动游戏：

1. 进入城镇 — 你看到"去酒馆"选项。
2. 点击 — 你进入了带有对话和选择的 `my_alko_bar` 场景。
3. 选择"离开" — 返回城镇。

---

## 下一步？

| 想要 | 前往 |
|---|---|
| 掌握所有 DSL 命令 | [DSL 命令参考](site/docs/mods/scenes/scenarios/dsl/) |
| 添加交互式对象 | [交互式对象](site/docs/mods/interactives/) |
| 编写 C# 模组 | [等级 3 — 程序员](#quickstart/programmer) |

---

## 下载完整示例

<a href="examples/quickstart_modder.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
下载 quickstart_modder.zip
</a>

可下载的模组为英文版本。
