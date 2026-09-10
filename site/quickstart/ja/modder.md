# レベル 2 — モッダー：DSLシーン

<div class="qs-meta">
<span>JSON + DSL</span>
<span>15分</span>
<span>コード不要</span>
</div>

独自のシーンを持つModを作成します — ダイアログ、選択肢、遷移のある部屋です。さらに、元のファイルに触れることなく、既存のキッチンシーンに「酒場へ行く」オプションを追加します。

---

## ステップ 1：Modの構造

フォルダと基本ファイルを作成：

```filestree
mods/
└── quickstart_modder/
    ├── manifest.json
    ├── scenes/
    │   └── my_alko_bar.json              # 新しいロケーション — 酒場！
    ├── scenarios/
    │   ├── my_alko_bar.scenario          # 酒場内のシナリオ、プレイヤーが新しいロケーションと対話できるように
    │   └── town_entry_override.scenario  # 町のシーンを上書きして新しいロケーションへの遷移を追加
    ├── backgrounds/
    │   └── my_alko_bar/
    │       └── bg.png                    # 酒場の内装画像
    ├── backgrounds/
    │   └── sounds/
    │       └── alko_bar_ambient.ogg      # 酒場の環境音
    └── locale/
        └── ru.properties                 # ロシア語ローカライゼーション
```

---

## ステップ 2：manifest.json

`mods/quickstart_modder/manifest.json`：

```jsonc
{
  "id": "alko_bar",                    // Mod ID
  "name": "私の酒場",                   // Modメニューに表示されるMod名
  "description": "町に酒場シーンを追加",  // Modメニューに表示されるModの説明
  "author": "modder",                 // Mod作者
  "depends": ["core"]                 // コアゲームへの依存（手動でMod読み込み順序を設定する必要なし）
}
```

---

## ステップ 3：シーン定義

`mods/quickstart_modder/scenes/my_alko_bar.json`：

```jsonc
{
  "name": "my_alko_bar",                              // 新しい一意のシーン識別子を作成（switch_sceneで使用）、これが新しいシーンになります。
  "defaultBackground": "my_alko_bar/bg",              // backgrounds/ 内のPNG背景へのパス（拡張子なし）
  "defaultAmbient": "locations/alkobar/bar_ambient",  // このロケーションの環境音ID（拡張子なし）、例：シーンに入ると再生される「酒場の騒音」
  "defaultScenario": "scenarios/my_alko_bar.scenario" // 他に何も指定されていない場合にこのシーンで実行されるデフォルトシナリオ
}
```

| フィールド | 説明 |
|---|---|
| `name` | 一意のシーンID。`switch_scene` で使用 |
| `defaultBackground` | `backgrounds/` 内の背景へのパス（拡張子なし） |
| `defaultAmbient` | 環境音。空文字列 — 音なし |
| `defaultScenario` | シーン進入時に実行される `.scenario` ファイルへのパス |

---

## ステップ 4：背景

`mods/quickstart_modder/backgrounds/my_alko_bar/bg.png` に画像を配置します。

任意のPNG画像が使用可能です。ゲームはシェーダーベースのライティングと時間帯をサポートしており — 背景は自動的に照らされます。

> 画像がない場合は、一時的に `mods/core/backgrounds/home/kitchen.png` → `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png` をコピーしてください。

---

## ステップ 5：DSLシナリオ

`mods/quickstart_modder/scenarios/my_alko_bar.scenario`：

```scenario
scene: my_alko_bar
priority: 10
---
text "あなたは半分空っぽの酒場に入る。"
text "退屈そうなバーテンダーがカウンターの後ろに立っている。"

select "[どうしますか？]"
option "カウンターに近づく" -> :bar
option "立ち去る" -> :leave

:bar
text "バーテンダーがうなずく。"
text "— ご注文は？"
end

:leave
switch_scene town_entry
end
```

### ここで何が起きているか

- **`text`** — ダイアログ行を表示します。`key=` パラメータはローカライゼーションキーです。
- **`select`** — 選択メニューを開きます。引用符内のテキストはプロンプトです。
- **`option`** — 選択肢。`-> :label` — 選択時にジャンプする場所。
- **`:bar`**、**`:leave`** — ジャンプ用のラベル。
- **`switch_scene`** — プレイヤーを別のシーンに移動します。
- **`end`** — シナリオを終了します（プレイヤーはシーンに留まります）。

---

## ステップ 6：町の拡張（シーン優先度）

元の `core` ファイルに**触れずに**、町のシーンに「酒場へ行く」オプションを追加しましょう。

`mods/quickstart_modder/scenarios/town_entry_override.scenario`：

```scenario
scene: town_entry
priority: 1
---
select "今日の町はいつもと違う匂いがする..."
option "家に帰る" -> :home
option "酒場へ行く" -> :alko_bar

:home
switch_scene home_outside
end

:alko_bar
switch_scene my_alko_bar
end
```

### なぜこれが機能するのか

`town_entry` シーンには2つのシナリオがあります：
- `priority: 10` — デフォルト（`core` Modから）
- `priority: 1` — 私たちのもの（`quickstart_modder` から）

エンジンは**最も低い** `priority` のシナリオを実行します。私たちの `priority: 1` が勝ち — プレイヤーは新しいオプションを見ます。

> 詳細：[シーン優先度システム](site/docs/mods/scenes/)。

---

## ステップ 7：確認

ゲームを開始します：

1. 町に入る — 「酒場へ行く」オプションが表示されます。
2. クリック — ダイアログと選択肢のある `my_alko_bar` シーンにいます。
3. 「立ち去る」を選択 — 町に戻ります。

---

## 次は？

| やりたいこと | こちらへ |
|---|---|
| すべてのDSLコマンドをマスター | [DSLコマンドリファレンス](site/docs/mods/scenes/scenarios/dsl/) |
| インタラクティブオブジェクトを追加 | [インタラクティブオブジェクト](site/docs/mods/interactives/) |
| C# Modを作成 | [レベル 3 — プログラマー](#quickstart/programmer) |

---

## 完成例をダウンロード

<a href="examples/quickstart_modder.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
quickstart_modder.zip をダウンロード
</a>

ダウンロード可能なModは英語です。
