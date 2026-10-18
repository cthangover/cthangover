# レベル 3 — プログラマー：C# Mod

<div class="qs-meta">
<span>C#</span>
<span>20分</span>
<span>Roslynコンパイル</span>
</div>

ゲームにカスタムDSLアクションとメニュー設定を追加するC# Modを書きます。コードはゲーム起動時にRoslynでその場でコンパイルされます — Godotも `dotnet build` も不要です。

---

## ステップ 1：Modの構造

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

## ステップ 2：manifest.json

`mods/quickstart_programmer/manifest.json`：

```jsonc
{
  "id": "quickstart_programmer",                 // ModのID
  "name": "My Code Mod",                         // Mod一覧に表示されるMod名
  "description": "新しいアクションとMod設定",       // Mod一覧に表示されるModの説明
  "sources": ["src/*.cs"],                       // ソースを集めるパターン（すべての ".cs" ファイル）
  "author": "programmer",                        // 作者
  "depends": ["cthangover_game"]               // 本編ゲームへの依存（ロード順を手動で並べる必要がなくなる）
}
```

`sources` フィールドは、RoslynでコンパイルするC#ファイルを指定します。コンパイルエラー（ファイルと行番号付き）はログの `MODS_COMPILE` カテゴリに出力されます。

---

## ステップ 3：エントリポイント — IModInitializer

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

`IModInitializer` インターフェースはModのC#コードのライフサイクルのエントリポイントで、実装はリフレクションで検出されます。`OnModLoaded` は各Modのロード時に呼ばれ（自分のModだけではありません — `modId` にはロードされたModのidが入ります）、`OnModResourcesReady` はすべてのModとそのリソースのロード後に一度だけ呼ばれます。

> ログを扱うなら `GD.Print(...)` ではなく `GameLogger.Log(...)` を使ってください。

---

## ステップ 4：メニュー設定 — IModSettings

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

`GetDefinitions` はコントロールを記述し、`WriteValues`/`ReadValues` は値を対称的に書き込み・読み込みます。ディスクへの保存とUIはコアが担当します。Modのロード後、「Settings」→「Mod Settings」タブに、スライダーとトグル付きであなたのModが表示されます。

---

## ステップ 5：カスタムDSLアクション — IScenarioAction

`.scenario` ファイルで使える新しいアクション `my_effect` を追加します。`mods/quickstart_programmer/src/MyEffectAction.cs`：

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

これで、どの `.scenario` ファイルでも次のように書けます：

```scenario
action my_effect
```

エンジンはリフレクションでクラスを見つけ、`Run` を呼び出します。`context` を通じて、会話変数（`GetParam`）やゲームのサービス — クエスト、キャラクター、ライティング、シーン — にアクセスできます。

---

## ステップ 6：試すためのシナリオ

アクションの動作を見るため、プレイヤーが家の外に出たときに呼び出します。`mods/quickstart_programmer/scenarios/my_effect_demo.scenario`：

```scenario
scene: home_outside
priority: 0
---
action my_effect
end
```

`priority: 0` — 私たちのシナリオは標準のもの（`priority: 10`）より先に実行され、`end` の後はいつもどおりゲームが続きます：シーンは通常のメニューを開きます。

---

## ステップ 7：ローカライゼーション

設定のラベルは `Name` のローカライズキーです。`mods/quickstart_programmer/locale/ru.properties`：

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

## ステップ 8：Modをゲームに接続する

ゲームはインストール済みのModをすべて読み込むわけではなく、ゲームのコレクションに含まれるModだけを読み込みます（ゲームのUIは英語とロシア語です）：

1. メインメニューで **「Mods & Collections」** → **「Collections」** タブを開きます。
2. **Cthangover** のゲームカードで **「Edit scheme」** を押します。
3. スキーム上の任意のメカニクス（例：**「Story」**）を選択し、**「Add mod」** を押します。
4. リストから自分のMod（`manifest.json` の名前）を見つけて **「Add」** を押します。
5. **「Save」** を押して確定します。変更は次回のゲーム起動時に反映されます。

> **開発者向け。** ソースからメニューなしで：`godot --path . -- --game=cthangover --new --with-mods=quickstart_programmer --scene=home_outside`。

---

## ステップ 9：確認

ゲームを再起動します：

1. 「Settings」→「Mod Settings」タブを開くと、スライダーとトグル付きのあなたのModがあります。
2. ゲームのログ（`user://logs/cthangover.log`、Windowsでは `%APPDATA%\cthangover\logs\cthangover.log`）に、ロード後 `[MY_MOD] My mod loaded!` が出力されます。
3. 家の外に出ると、ログに `[MY_MOD] New action executed!` が出ます。

---

## 次は？

| やりたいこと | 行き先 |
|---|---|
| シーンの入退場を購読する | [Scene Subscriptions](site/docs/mods/configs/) |
| 独自のバトルモードを作る | [Battle System](site/docs/mods/battle/) |
| ツールウィンドウを作る | [Creating a Custom Tool](site/docs/mods/editor_tools/tutorial) |
| GDScriptでModを書く | [GDScript in Mods](site/docs/mods/src/gdscript/) |
| 利用可能なすべてのインターフェース | [C# Source Code in Mods](site/docs/mods/src/) |

---

## 完成例をダウンロード

<a href="examples/quickstart_programmer.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
quickstart_programmer.zip をダウンロード
</a>

ダウンロード可能なModは英語です。アーカイブを `mods/quickstart_programmer/` に展開するか、`.zip` を `user://mods/` に置き、Modをゲームに接続してください（ステップ 8）。
