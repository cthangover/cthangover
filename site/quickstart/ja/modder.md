# レベル 2 — モッダー：DSLシーン

<div class="qs-meta">
<span>JSON + DSL</span>
<span>15分</span>
<span>コード不要</span>
</div>

会話・選択肢・遷移を持つ独自のシーン — バー — を含むModを作ります。さらに、元のファイルに触れずに、既存の町のシーンへバーの入口を追加します。

---

## ステップ 1：Modの構造

フォルダと基本ファイルを作成します：

```filestree
mods/
└── quickstart_modder/
    ├── manifest.json
    ├── scenes/
    │   └── my_alko_bar.json              # 新しいロケーション — 酒場！
    ├── scenarios/
    │   ├── my_alko_bar.scenario          # バーの中のシナリオ。新しいロケーションでプレイヤーが行動できるようにする
    │   └── town_entry_override.scenario  # 町のシーンを拡張して、新しいロケーションへの遷移を追加する
    ├── backgrounds/
    │   └── my_alko_bar/
    │       └── bg.png                    # バーの内装画像
    └── sounds/
        └── locations/
            └── alkobar/
                └── bar_ambient.ogg       # バーの環境音
```

---

## ステップ 2：manifest.json

`mods/quickstart_modder/manifest.json`：

```jsonc
{
  "id": "quickstart_modder",                    // ModのID
  "name": "マイ酒場",                            // Modメニューに表示されるMod名
  "description": "町に酒場のシーンを追加する",     // Modメニューに表示されるModの説明
  "author": "modder",                           // Modの作者
  "depends": ["cthangover_game"]              // 本編ゲームへの依存（ロード順を手動で並べる必要がなくなる）
}
```

---

## ステップ 3：シーン定義

`mods/quickstart_modder/scenes/my_alko_bar.json`：

```jsonc
{
  "name": "my_alko_bar",                              // 新しい一意なシーン識別子（switch_scene で使用）。これが新しいシーンになる
  "defaultBackground": "my_alko_bar/bg",              // backgrounds/ 内のPNG背景へのパス（拡張子なし）
  "defaultAmbient": "locations/alkobar/bar_ambient",  // このロケーションの環境音ID（sounds/ 内のパス、拡張子なし）。例：入場時に流れる「バーの喧騒」
  "defaultScenario": "scenarios/my_alko_bar.scenario" // 他に何もなければこのシーンで実行されるデフォルトシナリオ
}
```

| フィールド | 説明 |
|---|---|
| `name` | シーンの一意なID。`switch_scene` で使用 |
| `defaultBackground` | `backgrounds/` 内の背景へのパス（拡張子なし） |
| `defaultAmbient` | 環境音：`sounds/` 内のパス（拡張子なし）。空文字列 — 音なし |
| `defaultScenario` | 入場時に実行される `.scenario` ファイルへのパス |

---

## ステップ 4：背景と音

画像を `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png` に、環境音を `mods/quickstart_modder/sounds/locations/alkobar/bar_ambient.ogg` に置きます。

どんなPNG画像でも構いません。ゲームはシェーダーによるライティングと時間帯に対応しており、背景は自動的にライティングされます。

> 自分の画像がない場合は、一時的に `mods/cthangover_game/backgrounds/home/kitchen.png` → `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png` とコピーしてください。音なしでも構いません：`"defaultAmbient": ""` と指定します。

---

## ステップ 5：DSLシナリオ

`mods/quickstart_modder/scenarios/my_alko_bar.scenario`：

```scenario
scene: my_alko_bar
priority: 10
---
text "あなたは半分空いたバーに入る。"
text "カウンターの向こうでバーテンダーが退屈そうにしている。"

:menu
select "[どうする？]"
option "カウンターへ行く" -> :bar
option "立ち去る" -> :leave

:bar
text "バーテンダーがうなずく。"
text "「何にしますか？」"
goto -> :menu

:leave
switch_scene town_entry
end
```

### ここで何が起きているか

- **`scene:`** / **`priority:`** — ヘッダー：シナリオがどのシーンに属し、どの順番で実行されるか。ヘッダーと本体は `---` の行で区切ります。
- **`text`** — 会話の一行を表示します。パラメータ `key=` はローカライズキーです（ここでは不要：なければテキスト自体が表示されます）。
- **`select`** — 選択メニューを開きます。引用符内のテキストはプロンプトです。
- **`option`** — 選択肢です。`-> :label` — 選んだときのジャンプ先。
- **`:menu`**、**`:bar`**、**`:leave`** — ジャンプ用のラベル。
- **`goto -> :menu`** — 無条件ジャンプ：バーテンダーと話した後、再びメニューが開きます。
- **`switch_scene`** — プレイヤーを別のシーンへ移動させます。
- **`end`** — シナリオを終了します。

---

## ステップ 6：町の拡張（シーン優先度）

次に、`cthangover_game` の元のファイルに**触れずに**、町のシーンへバーの入口を追加します。

`mods/quickstart_modder/scenarios/town_entry_override.scenario`：

```scenario
scene: town_entry
priority: 1
---
select "半開きのバーの扉から音楽が漏れてくる…"
option "バーに入る" -> :alko_bar
option "通り過ぎる" -> :pass

:alko_bar
switch_scene my_alko_bar
end

:pass
end
```

### なぜこれが機能するのか

`town_entry` シーンには今、複数のシナリオがあります：

| priority | シナリオ | Mod |
|---|---|---|
| `1` | 私たちの `town_entry_override` | `quickstart_modder` |
| `10`–`30` | ムラカミとの出会い（その `condition:` が満たされている間のみ） | `cthangover_murakami` |
| `40` | 標準の町メニュー | `cthangover_game` |

シーンに入ると、エンジンは `priority` の昇順 — **最も小さい**値から — で、条件を満たす最初のシナリオを実行します。シナリオがシーンを変えずに終了すると、次のシナリオが始まります。つまり：

- 「バーに入る」 — `switch_scene` がプレイヤーをバーへ連れて行きます；
- 「通り過ぎる」 — 私たちのシナリオは単に終了し（`end`）、その後は元のゲームどおりに進みます：ムラカミのストーリーシーンか、標準の町メニューです。

こうしてModは何も壊さずに新しいものを追加します。もし私たちのシナリオが町のメニューを自前で描いたら、ストーリーも他のModも覆い隠してしまうでしょう。

> 詳細：[Scene Priority System](site/docs/mods/scenes/)。

---

## ステップ 7：Modをゲームに接続する

ゲームはインストール済みのModをすべて読み込むわけではなく、ゲームのコレクションに含まれるModだけを読み込みます。Modを追加してください（ゲームのUIは英語とロシア語です）：

1. メインメニューで **「Mods & Collections」** → **「Collections」** タブを開きます。
2. **Cthangover** のゲームカードで **「Edit scheme」** を押します。
3. スキーム上の任意のメカニクス（例：**「Story」**）を選択し、**「Add mod」** を押します。
4. リストから自分のMod（`manifest.json` の名前）を見つけて **「Add」** を押します。
5. **「Save」** を押して確定します。変更は次回のゲーム起動時に反映されます。

---

## ステップ 8：確認

ゲームを再起動します：

1. 町に入ると、最初に「バーに入る」/「通り過ぎる」の選択肢が表示されます。
2. 「バーに入る」を押すと — 背景、バーの喧騒、会話、選択肢のある自分の `my_alko_bar` シーンにいます。
3. 「立ち去る」を選ぶと — 町に戻ります。
4. 「通り過ぎる」を選ぶと — ゲームはいつもどおり続きます。

> **開発者向け。** ソースからなら、プロローグをプレイせずにすぐシーンを開けます：`godot --path . -- --game=cthangover --new --with-mods=quickstart_modder --scene=town_entry`。

---

## 次は？

| やりたいこと | 行き先 |
|---|---|
| すべてのDSLコマンドを習得する | [DSL Command Reference](site/docs/mods/scenes/scenarios/dsl/) |
| インタラクティブオブジェクトを追加する | [Interactive Objects](site/docs/mods/interactives/) |
| C# Modを書く | [レベル 3 — プログラマー](#quickstart/programmer) |

---

## 完成例をダウンロード

<a href="examples/quickstart_modder.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
quickstart_modder.zip をダウンロード
</a>

ダウンロード可能なModのセリフはロシア語です。アーカイブを `mods/quickstart_modder/` に展開するか、`.zip` を `user://mods/` に置き、Modをゲームに接続してください（ステップ 7）。
