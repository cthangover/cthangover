# レベル 1 — プレイヤー：キャラクターのパッチ

<div class="qs-meta">
<span>テキストエディタ</span>
<span>5分</span>
<span>コード不要</span>
</div>

パッチシステムを使って、キャラクター Marao を強化します — HPと攻撃力を上げ、「スタン」スキルを追加します。コードは一行も書きません。JSONだけです。

---

## ステップ 1：Modフォルダを作成

各Modは `mods/`（ゲームの隣）または `user://mods/`（ユーザーデータフォルダ。Windowsでは `%APPDATA%\cthangover\mods`）の中に専用フォルダを持ちます。次のように作成します：

```filestree
mods/
└── quickstart_player/
    ├── manifest.json
    └── patches/
        └── character.json
```

---

## ステップ 2：manifest.json

すべてのModにはマニフェストが必要です。`mods/quickstart_player/manifest.json` を作成します：

```jsonc
{
  "id": "quickstart_player",        // Modの一意な識別子。英語で書く
  "name": "Marao Buff",             // Mod管理メニューに表示される名前
  "description": "初めてのMod！",    // Mod一覧とカタログに表示される説明
  "author": "player",               // Modの作者（任意の文字列）
  "depends": ["cthangover_game"]  // 本編ゲームへの依存（ロード順を手動で並べる必要がなくなる）
}
```

マニフェストファイル自体は必須です — ないModはスキップされます — が、厳密に必須のフィールドはありません：`id` を省略するとフォルダ名がModの識別子になります。それでも `id` と `name` は書いておくことを推奨します。ゲームはModフォルダを自動で見つけますが、Modがゲームに入るのは接続した後です（ステップ 4）。

---

## ステップ 3：キャラクターパッチ

`mods/quickstart_player/patches/character.json` を作成します：

```jsonc
{
  "Items": [
    {
      "Id": "Marao", // パッチ対象のキャラクターID（本編Modで定義）

      "Health": 5000, // 新しいプロパティ値 — 元の値を上書きする
      "Attack": 1200,
      "Point": 2,

      "Actions$add": ["physics/stun"] // $add は配列に要素を追加する
    }
  ]
}
```

### 仕組み

- **`"Id": "Marao"`** — パッチの対象：識別子 `Marao` のキャラクター（`cthangover_game` Modで定義）。
- **`"Health": 5000`**、**`"Attack": 1200`**、**`"Point": 2`** — キャラクターのフィールドを上書きします。新しい値が古い値を置き換えます。
- **`"Actions$add": ["physics/stun"]`** — サフィックス `$add` は「配列に要素を追加する」という意味です。アクション `physics/stun` がリストに追加され、標準の `physics/attack` と `physics/defence` はそのまま残ります。スタンには 2 AP が必要なので、`Point` も上げています。

> パッチの詳細：[Patches](site/docs/mods/patches/) と [Array Operations](site/docs/mods/patches/arrays-and-identity)。

---

## ステップ 4：Modをゲームに接続する

ゲームはインストール済みのModをすべて読み込むわけではなく、ゲームのコレクションに含まれるModだけを読み込みます。新しいModを一度だけ追加してください（ゲームのUIは英語とロシア語です）：

1. メインメニューで **「Mods & Collections」** → **「Collections」** タブを開きます。
2. **Cthangover** のゲームカードで **「Edit scheme」** を押します。
3. スキーム上の任意のメカニクス（例：**「Story」**）を選択し、**「Add mod」** を押します。
4. リストから自分のMod（`manifest.json` の名前）を見つけて **「Add」** を押します。
5. **「Save」** を押して確定します。変更は次回のゲーム起動時に反映されます。

> **開発者向け。** ソースから実行する場合は、メニューを使わずフラグで接続できます：`godot --path . -- --game=cthangover --new --with-mods=quickstart_player`。

---

## ステップ 5：実行して確認

ゲームを再起動し、新しいゲームを始めます。どの戦闘に入っても、Marao は次のようになっています：

- HP 16 → **5000**
- ATK 5 → **1200**
- AP 1 → **2**
- 「Basic attack」「Defence」の隣に3つ目のアクション **「Stun」**（スタン）

---

## 次は？

| やりたいこと | 行き先 |
|---|---|
| 会話付きの独自シーンを作る | [レベル 2 — モッダー](#quickstart/modder) |
| パッチシステムを深く理解する | [Patches](site/docs/mods/patches/) |
| キャラクターについて学ぶ | [Characters](site/docs/mods/characters/) |
| 初めてのC# Modを書く | [レベル 3 — プログラマー](#quickstart/programmer) |

---

## 完成例をダウンロード

<a href="examples/quickstart_player.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
quickstart_player.zip をダウンロード
</a>

ダウンロード可能なModは英語です。インストールするには、アーカイブをゲームの `mods/quickstart_player/` フォルダに展開するか、`.zip` をそのまま `user://mods/` に置いてください：ゲームはアーカイブから直接Modを読み込み、`user://mods/` のModは同じidの組み込みModより優先されます。インストール後、Modをゲームに接続してください（ステップ 4）。
