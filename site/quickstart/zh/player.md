# 等级 1 — 玩家：修改角色

<div class="qs-meta">
<span>文本编辑器</span>
<span>5 分钟</span>
<span>无需代码</span>
</div>

你将增强角色 Marao — 提高生命值、攻击力，并添加"眩晕"技能 — 使用补丁系统。零代码，只需 JSON。

---

## 步骤 1：创建模组文件夹

每个模组存在于 `mods/` 内的独立文件夹中。创建：

```filestree
mods/
└── quickstart_player/
    ├── manifest.json
    └── patches/
        └── characters.json
```

---

## 步骤 2：manifest.json

每个模组都需要清单文件。创建 `mods/quickstart_player/manifest.json`：

```jsonc
{
  "id": "quickstart_player",      // 唯一模组标识符，请使用英文
  "name": "Marao 强化",            // 在模组管理菜单中显示的名称
  "description": "我的第一个模组！", // 在模组列表和目录中显示的描述
  "author": "player",             // 模组作者（任意字符串）
  "depends": ["core"]             // 依赖核心游戏（无需手动排列模组加载顺序）
}
```

最小的清单只需要 `name`，但最好也填写 `id`。内核会自动发现该文件夹并将其作为模组加载。

---

## 步骤 3：角色补丁

创建 `mods/quickstart_player/patches/characters.json`：

```jsonc
{
  "Items": [
    {
      "Id": "Marao",  // 要补丁的角色 ID（在 core 模组中定义）

      "Health": 5000, // 新属性值 — 覆盖原始值
      "Attack": 1200,
      "Points": 2,

      "Actions$add": ["physics/stun"] // $add 向数组追加元素
    }
  ]
}
```

### 工作原理

- **`"Id": "Marao"`** — 补丁目标：标识符为 `Marao` 的角色（在 `core` 模组中定义）。
- **`"Health": 5000`**、**`"Attack": 1200`**、**`"Points": 2`** — 覆盖角色字段。新值替换旧值。
- **`"Actions$add": ["physics/stun"]`** — 后缀 `$add` 表示"追加到数组"。动作 `physics/stun` 被添加到列表中。默认的 `physics/attack` 和 `physics/defence` 保持不变。

> 了解更多关于补丁：[Patches](site/docs/mods/patches/) 和 [数组操作](site/docs/mods/patches/arrays-and-identity)。

---

## 步骤 4：运行并检查

启动游戏。进入任意战斗 — Marao 现在拥有：

- **5000 HP** 替代 10
- **1200 ATK** 替代 5
- **2 行动点** 替代 1
- 第三个动作 **"眩晕"** 与"攻击"和"防御"并列

---

## 下一步？

| 想要 | 前往 |
|---|---|
| 创建带对话的自定义场景 | [等级 2 — 模组制作者](#quickstart/modder) |
| 深入了解补丁系统 | [Patches](site/docs/mods/patches/) |
| 了解角色系统 | [Characters](site/docs/mods/characters/) |
| 编写第一个 C# 模组 | [等级 3 — 程序员](#quickstart/programmer) |

---

## 下载完整示例

<a href="examples/quickstart_player.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
下载 quickstart_player.zip
</a>

可下载的模组为英文版本。
