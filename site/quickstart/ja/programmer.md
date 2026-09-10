# レベル 3 — プログラマー：C# Mod

<div class="qs-meta">
<span>C#</span>
<span>20分</span>
<span>Roslynコンパイル</span>
</div>

カスタムDSLアクションとメニュー設定をゲームに追加するC# Modを作成します。コードはRoslynによってオンザフライでコンパイルされます — Godotは不要です。

---

## ステップ 1：Modの構造

```filestree
mods/
└── quickstart_programmer/
    ├── manifest.json
    ├── src/
    │   ├─── MyModSettings.cs
    │   ├─── MyMod.cs
    │   └─── MyEffectAction.cs
    └── locale/
        └─── ja.properties
```

---

## ステップ 2：manifest.json

`mods/quickstart_programmer/manifest.json`：

```jsonc
{
  "id": "quickstart_programmer",           // Mod ID
  "name": "My Code Mod",                   // Modリストに表示されるMod名
  "description": "新しいアクションとMod設定",  // Modリストに表示されるModの説明
  "sources": ["src/*.cs"],                 // ソースファイルを収集するグロブパターン（すべての ".cs" ファイルを含む）
  "author": "programmer",                  // 作者
  "depends": ["core"]                      // コアゲームへの依存（手動でMod読み込み順序を設定する必要なし）
}
```

`sources` フィールドは、RoslynでコンパイルするC#ファイルを指定します。

---

## ステップ 3：エントリポイント — IMod

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

            GameLogger.Log("MY_MOD", "Modがロードされました！", LogLevel.Message);
        }

    }
}
```

`IMod` インターフェースはすべてのC# Modに必須のエントリポイントです。`Initialize` メソッドはModのロード時に呼び出されます。

> ログを扱う場合は、`GD.Print(...)` の代わりに `GameLogger.Log(...)` を使用してください。

---

## ステップ 4：メニュー設定 — IModSettings

`MyModSettings.cs` に追加：

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

Modのロード後、「設定 → Mod Settings」に移動すると — ボリュームスライダーとトグルが表示されます。

---

## ステップ 5：カスタムDSLアクション — IScenarioAction

`.scenario` ファイルで使用できる新しい `my_effect` アクションを追加しましょう。`MyEffectAction.cs` を作成：

```csharp
using Cthangover.Core.Scenarios;
using Cthangover.Core.Actions;
using Cthangover.Core.Utils;

public class MyEffectAction : IScenarioAction
{
    public string Name => "my_effect";

    public void Run(IActionContext context)
    {
        GameLogger.Log("MY_MOD", "新しいアクションが実行されました！", LogLevel.Message);
    }
}
```

これで任意の `.scenario` ファイルに以下のように書けます：

```scenario
action my_effect
```

エンジンはリフレクションでクラスを見つけ、`Execute` を呼び出します。

---

## ステップ 6：ローカライゼーション

`mods/quickstart_programmer/locale/ja.properties`：

```properties
mymod/effect_volume = エフェクト音量
mymod/enable_mod = Modを有効にする
```

---

## ステップ 7：確認

ゲームを開始します：

1. 設定メニューを開く → "Mod Settings" タブ → スライダーとチェックボックス付きのModが表示されます。
2. `action my_effect` コマンドを含むテスト用 `.scenario` を作成します。
3. ゲームログに `[MY_MOD] 新しいアクションが実行されました！` と表示されます。

---

## 次は？

| やりたいこと | こちらへ |
|---|---|
| シーンの入退場をサブスクライブ | [シーンサブスクリプション](site/docs/mods/configs/) |
| 独自のバトルモードを作成 | [バトルシステム](site/docs/mods/battle/) |
| ツールウィンドウを作成 | [カスタムツールの作成](site/docs/mods/tools/tutorial) |
| GDScriptでModを作成 | [ModでのGDScript](site/docs/mods/src/gdscript/) |
| 利用可能なすべてのインターフェース | [ModでのC#ソースコード](site/docs/mods/src/) |

---

## 完成例をダウンロード

<a href="examples/quickstart_programmer.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
quickstart_programmer.zip をダウンロード
</a>

ダウンロード可能なModは英語です。
