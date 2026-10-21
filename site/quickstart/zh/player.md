# 等级 1 — 玩家：修改角色

<div class="qs-meta">
<span>文本编辑器</span>
<span>5 分钟</span>
<span>无需代码</span>
</div>

你将通过补丁系统强化角色 Marao — 提高生命值和攻击力，并添加"眩晕"能力。无需一行代码，只需 JSON。

---

## 步骤 1：创建模组文件夹

每个模组都位于 `mods/`（游戏旁边）或 `user://mods/`（用户数据文件夹；Windows 上为 `%APPDATA%\cthangover\mods`）中的独立文件夹里。创建：

```filestree
mods/
└── quickstart_player/
    ├── manifest.json
    └── patches/
        └── character.json
```

---

## 步骤 2：manifest.json

每个模组都需要清单文件。创建 `mods/quickstart_player/manifest.json`：

```jsonc
{
  "id": "quickstart_player",       // 唯一模组标识符，请使用英文
  "name": "Marao Buff",            // 在模组管理菜单中显示的名称
  "description": "我的第一个模组！",  // 在模组列表和目录中显示的描述
  "author": "player",              // 模组作者（任意字符串）
  "depends": ["cthangover_game"] // 依赖主游戏的内容（无需手动排列模组加载顺序）
}
```

manifest.json 文件本身是必需的 — 缺少它的模组会被跳过 — 但没有任何单个字段是严格必需的：如果省略 `id`，文件夹名将作为模组 ID 使用。不过仍建议填写 `id` 和 `name`。游戏会自动找到模组文件夹，但只有在连接之后模组才会进入游戏（步骤 4）。

---

## 步骤 3：角色补丁

创建 `mods/quickstart_player/patches/character.json`：

```jsonc
{
  "Items": [
    {
      "Id": "Marao",  // 要补丁的角色 ID（在核心模组中定义）

      "Health": 5000, // 新属性值 — 覆盖原始值
      "Attack": 1200,
      "Point": 2,

      "Actions$add": ["physics/stun"] // $add 向数组追加元素
    }
  ]
}
```

### 工作原理

- **`"Id": "Marao"`** — 补丁目标：标识符为 `Marao` 的角色（在 `cthangover_game` 模组中定义）。
- **`"Health": 5000`**、**`"Attack": 1200`** 和 **`"Point": 2`** — 覆盖角色的字段。新值替换旧值。
- **`"Actions$add": ["physics/stun"]`** — 后缀 `$add` 表示"向数组追加元素"。动作 `physics/stun` 被添加到列表中，默认的 `physics/attack` 和 `physics/defence` 保留。眩晕需要 2 AP — 所以我们也提高了 `Point`。

> 了解更多补丁信息：[Patches](site/docs/mods/patches/) 和 [Array Operations](site/docs/mods/patches/arrays-and-identity)。

---

## 步骤 4：将模组连接到游戏

游戏不会加载所有已安装的模组，只加载属于游戏合集（collection）的模组。把新模组添加进去一次即可（游戏界面提供英语和俄语）：

1. 在主菜单中打开 **"Mods & Collections"** → **"Collections"** 标签页。
2. 在 **Cthangover** 游戏卡片上点击 **"Edit scheme"**。
3. 在方案图上选中任意机制（例如 **"Story"**），然后点击 **"Add mod"**。
4. 在列表中找到你的模组（按 `manifest.json` 中的名称），点击 **"Add"**。
5. 点击 **"Save"** 并确认。更改将在下次启动游戏时生效。

> **开发者提示。** 从源码运行时，可以不用菜单，直接用参数连接模组：`godot --path . -- --game=cthangover --new --with-mods=quickstart_player`。

---

## 步骤 5：运行并检查

重启游戏并开始新游戏。进入任意战斗 — Marao 现在拥有：

- **5000 HP**（原为 16）
- **1200 ATK**（原为 5）
- **2 AP**（原为 1）
- 在"Basic attack"和"Defence"旁边出现第三个动作 **"Stun"**（眩晕）

---

## 下一步？

| 想要 | 前往 |
|---|---|
| 创建带对话的自定义场景 | [等级 2 — 模组制作者](#quickstart/modder) |
| 深入理解补丁系统 | [Patches](site/docs/mods/patches/) |
| 了解角色 | [Characters](site/docs/mods/characters/) |
| 编写你的第一个 C# 模组 | [等级 3 — 程序员](#quickstart/programmer) |

---

## 下载完整示例

<a href="examples/quickstart_player.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
下载 quickstart_player.zip
</a>

可下载的模组为英文版本。安装方法：将压缩包解压到游戏的 `mods/quickstart_player/` 文件夹 — 或者直接把 `.zip` 放进 `user://mods/`：游戏可以直接从压缩包读取模组，且 `user://mods/` 中的模组优先于同 id 的内置模组。安装后，请将模组连接到游戏（步骤 4）。
