# Cấp 3 — Lập Trình Viên: Mod C#

<div class="qs-meta">
<span>C#</span>
<span>20 phút</span>
<span>Biên dịch Roslyn</span>
</div>

Bạn sẽ viết một mod C# thêm vào trò chơi một hành động DSL tùy chỉnh và các cài đặt trong menu. Code được biên dịch tức thì bằng Roslyn khi trò chơi khởi động — không cần Godot hay `dotnet build`.

---

## Bước 1: Cấu trúc mod

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

## Bước 2: manifest.json

`mods/quickstart_programmer/manifest.json`:

```jsonc
{
  "id": "quickstart_programmer",                   // ID mod
  "name": "My Code Mod",                           // Tên mod hiển thị trong danh sách mod
  "description": "Hành động mới và cài đặt mod",    // Mô tả mod hiển thị trong danh sách mod
  "sources": ["src/*.cs"],                         // Mẫu để thu thập mã nguồn (tất cả tệp ".cs")
  "author": "programmer",                          // Tác giả
  "depends": ["cthangover_game"]                 // Phụ thuộc vào trò chơi chính (không cần tự sắp xếp thứ tự tải mod)
}
```

Trường `sources` chỉ định những tệp C# cần biên dịch bằng Roslyn. Lỗi biên dịch (kèm tệp và dòng) được ghi vào log trong danh mục `MODS_COMPILE`.

---

## Bước 3: Điểm vào — IModInitializer

`mods/quickstart_programmer/src/MyMod.cs`:

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

Interface `IModInitializer` là điểm vào vòng đời của code C# trong mod; các triển khai được phát hiện qua reflection. `OnModLoaded` được gọi khi mỗi mod được tải (không chỉ mod của bạn — `modId` chứa id của mod vừa tải), `OnModResourcesReady` — một lần sau khi tất cả mod và tài nguyên của chúng đã được tải.

> Hãy dùng `GameLogger.Log(...)` thay cho `GD.Print(...)` nếu bạn muốn làm việc với log.

---

## Bước 4: Cài đặt menu — IModSettings

`mods/quickstart_programmer/src/MyModSettings.cs`:

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

`GetDefinitions` mô tả các điều khiển, `WriteValues`/`ReadValues` ghi và đọc giá trị một cách đối xứng; lõi trò chơi lo việc lưu xuống đĩa và giao diện. Sau khi mod được tải, mod của bạn xuất hiện trong "Settings" → tab "Mod Settings" với một thanh trượt và một công tắc.

---

## Bước 5: Hành động DSL tùy chỉnh — IScenarioAction

Hãy thêm hành động mới `my_effect`, dùng được trong các tệp `.scenario`. `mods/quickstart_programmer/src/MyEffectAction.cs`:

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

Giờ trong bất kỳ tệp `.scenario` nào bạn cũng có thể viết:

```scenario
action my_effect
```

Engine sẽ tìm lớp của bạn qua reflection và gọi `Run`. Thông qua `context` bạn truy cập được biến hội thoại (`GetParam`) và các dịch vụ của trò chơi: nhiệm vụ, nhân vật, ánh sáng, cảnh.

---

## Bước 6: Kịch bản để thử

Để thấy hành động hoạt động, ta sẽ gọi nó khi người chơi bước ra khỏi nhà. `mods/quickstart_programmer/scenarios/my_effect_demo.scenario`:

```scenario
scene: home_outside
priority: 0
---
action my_effect
end
```

`priority: 0` — kịch bản của chúng ta chạy trước kịch bản tiêu chuẩn (`priority: 10`), và sau `end` trò chơi tiếp tục như bình thường: cảnh mở menu thông thường của nó.

---

## Bước 7: Bản địa hóa

Nhãn của các cài đặt là các khóa bản địa hóa trong `Name`. `mods/quickstart_programmer/locale/ru.properties`:

```properties
mymod/effect_volume = Громкость эффекта
mymod/enable_mod = Включить мод
```

`mods/quickstart_programmer/locale/en.properties`:

```properties
mymod/effect_volume = Effect volume
mymod/enable_mod = Enable mod
```

---

## Bước 8: Kết nối mod với trò chơi

Trò chơi không tải mọi mod đã cài, mà chỉ tải các mod thuộc bộ sưu tập của trò chơi (giao diện trò chơi có tiếng Anh và tiếng Nga):

1. Trong menu chính, mở **"Mods & Collections"** → tab **"Collections"**.
2. Trên thẻ trò chơi **Cthangover**, nhấn **"Edit scheme"**.
3. Chọn bất kỳ cơ chế nào trên sơ đồ (ví dụ **"Story"**) và nhấn **"Add mod"**.
4. Tìm mod của bạn trong danh sách (theo tên trong `manifest.json`) và nhấn **"Add"**.
5. Nhấn **"Save"** và xác nhận. Thay đổi có hiệu lực ở lần khởi động trò chơi tiếp theo.

> **Dành cho nhà phát triển.** Từ mã nguồn, không cần menu: `godot --path . -- --game=cthangover --new --with-mods=quickstart_programmer --scene=home_outside`.

---

## Bước 9: Kiểm tra

Khởi động lại trò chơi:

1. Mở "Settings" → tab "Mod Settings" → mod của bạn với thanh trượt và công tắc.
2. Log của trò chơi (`user://logs/cthangover.log`, trên Windows — `%APPDATA%\cthangover\logs\cthangover.log`) có dòng `[MY_MOD] My mod loaded!` sau khi tải.
3. Bước ra khỏi nhà — trong log xuất hiện `[MY_MOD] New action executed!`.

---

## Tiếp theo là gì?

| Bạn muốn | Đi đến |
|---|---|
| Đăng ký sự kiện vào/ra cảnh | [Scene Subscriptions](site/docs/mods/configs/) |
| Tạo chế độ chiến đấu riêng | [Battle System](site/docs/mods/battle/) |
| Làm cửa sổ công cụ | [Creating a Custom Tool](site/docs/mods/editor_tools/tutorial) |
| Viết mod bằng GDScript | [GDScript in Mods](site/docs/mods/src/gdscript/) |
| Tất cả interface có sẵn | [C# Source Code in Mods](site/docs/mods/src/) |

---

## Tải xuống ví dụ hoàn chỉnh

<a href="examples/quickstart_programmer.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Tải xuống quickstart_programmer.zip
</a>

Mod tải về bằng tiếng Anh. Giải nén tệp vào `mods/quickstart_programmer/` hoặc đặt tệp `.zip` vào `user://mods/`, sau đó kết nối mod với trò chơi (bước 8).
