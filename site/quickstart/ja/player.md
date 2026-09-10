# レベル 1 — プレイヤー：キャラクターのパッチ

<div class="qs-meta">
<span>テキストエディタ</span>
<span>5分</span>
<span>コード不要</span>
</div>

パッチシステムを使って、キャラクター Marao を強化します — 体力と攻撃力を上げ、「スタン」アビリティを追加します。コードは一切不要、JSONだけです。

---

## ステップ 1：Modフォルダを作成

各Modは `mods/` 内の独自フォルダに配置されます。作成：

```filestree
mods/
└── quickstart_player/
    ├── manifest.json
    └── patches/
        └── characters.json
```

---

## ステップ 2：manifest.json

すべてのModにマニフェストが必要です。`mods/quickstart_player/manifest.json` を作成：

```jsonc
{
  "id": "quickstart_player",       // 一意のMod識別子、英語を使用
  "name": "Marao強化",              // Mod管理メニューに表示される名前
  "description": "初めてのMod！",    // Modリストとカタログに表示される説明
  "author": "player",              // Mod作者（任意の文字列）
  "depends": ["core"]              // コアゲームへの依存（手動でMod読み込み順序を設定する必要なし）
}
```

最小限のマニフェストは `name` だけで十分ですが、`id` も記入することをお勧めします。カーネルがフォルダを自動検出し、Modとしてロードします。

---

## ステップ 3：キャラクターパッチ

`mods/quickstart_player/patches/characters.json` を作成：

```jsonc
{
  "Items": [
    {
      "Id": "Marao",  // パッチ対象のキャラクターID（core Modで定義）

      "Health": 5000, // 新しいプロパティ値 — 元の値を上書き
      "Attack": 1200,
      "Points": 2,

      "Actions$add": ["physics/stun"] // $add は配列に要素を追加
    }
  ]
}
```

### 仕組み

- **`"Id": "Marao"`** — パッチの対象：識別子 `Marao` のキャラクター（`core` Modで定義）。
- **`"Health": 5000`**、**`"Attack": 1200`**、**`"Points": 2`** — キャラクターのフィールドを上書きします。新しい値が古い値を置き換えます。
- **`"Actions$add": ["physics/stun"]`** — 接尾辞 `$add` は「配列に追加」を意味します。アクション `physics/stun` がリストに追加されます。デフォルトの `physics/attack` と `physics/defence` はそのまま残ります。

> パッチの詳細：[Patches](site/docs/mods/patches/) と [配列操作](site/docs/mods/patches/arrays-and-identity)。

---

## ステップ 4：実行して確認

ゲームを起動します。任意の戦闘に入ると — Marao は次のように変わります：

- **5000 HP** （10の代わりに）
- **1200 ATK** （5の代わりに）
- **2 AP** （1の代わりに）
- 3つ目のアクション **「スタン」** が「攻撃」「防御」と並んで表示

---

## 次は？

| やりたいこと | こちらへ |
|---|---|
| ダイアログ付きの独自シーンを作成 | [レベル 2 — モッダー](#quickstart/modder) |
| パッチシステムを深く理解 | [Patches](site/docs/mods/patches/) |
| キャラクターについて学ぶ | [Characters](site/docs/mods/characters/) |
| 初めてのC# Modを作成 | [レベル 3 — プログラマー](#quickstart/programmer) |

---

## 完成例をダウンロード

<a href="examples/quickstart_player.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
quickstart_player.zip をダウンロード
</a>

ダウンロード可能なModは英語です。
