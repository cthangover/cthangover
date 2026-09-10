# Cấp 3 — Lập Trình Viên: Mod C#

<div class="qs-meta">
<span>C#</span>
<span>20 phút</span>
<span>Biên dịch Roslyn</span>
</div>

Bạn sẽ viết một mod C# thêm vào trò chơi: hành động DSL tùy chỉnh và cài đặt trong menu. Mã được biên dịch ngay lập tức qua Roslyn — không cần Godot.

---

## Bước 1: Cấu trúc mod

```filestree
mods/
└── quickstart_programmer/
    ├── manifest.json
    ├── src/
    │   ├─── MyModSettings.cs
    │   ├─── MyMod.cs
    │   └─── MyEffectAction.cs
    └── locale/
        └─── vi.properties
```

---

## Bước 2: manifest.json

`mods/quickstart_programmer/manifest.json`：

```jsonc
{
  "id": "quickstart_programmer",                 // ID mod
  "name": "My Code Mod",                         // Tên mod hiển thị trong danh sách mod
  "description": "Hành động mới và cài đặt mod", // Mô tả mod hiển thị trong danh sách mod
  "sources": ["src/*.cs"],                       // Mẫu glob để thu thập tệp nguồn (bao gồm tất cả tệp ".cs")
  "author": "programmer",                        // Tác giả
  "depends": ["core"]                            // Phụ thuộc vào game lõi (không cần sắp xếp thứ tự tải mod thủ công)
}
```

Trường `sources` chỉ định những tệp C# nào cần biên dịch qua Roslyn.

---

## Bước 3: Điểm vào — IMod

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

            GameLogger.Log("MY_MOD", "Mod của tôi đã được tải!", LogLevel.Message);
        }

    }
}
```

Giao diện `IMod` là điểm vào bắt buộc cho mọi mod C#. Phương thức `Initialize` được gọi khi mod tải.

> Sử dụng `GameLogger.Log(...)` thay vì `GD.Print(...)` nếu bạn muốn làm việc với nhật ký.

---

## Bước 4: Cài đặt menu — IModSettings

Thêm vào `MyModSettings.cs`：

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

Sau khi tải mod, vào "Cài đặt → Mod Settings" — mod của bạn xuất hiện với thanh trượt âm lượng và nút bật/tắt.

---

## Bước 5: Hành động DSL tùy chỉnh — IScenarioAction

Hãy thêm hành động `my_effect` mới, có thể dùng trong tệp `.scenario`. Tạo `MyEffectAction.cs`：

```csharp
using Cthangover.Core.Scenarios;
using Cthangover.Core.Actions;
using Cthangover.Core.Utils;

public class MyEffectAction : IScenarioAction
{
    public string Name => "my_effect";

    public void Run(IActionContext context)
    {
        GameLogger.Log("MY_MOD", "Hành động mới đã được thực thi!", LogLevel.Message);
    }
}
```

Giờ trong bất kỳ tệp `.scenario` nào bạn có thể viết：

```scenario
action my_effect
```

Engine tìm lớp của bạn qua reflection và gọi `Execute`.

---

## Bước 6: Bản địa hóa

`mods/quickstart_programmer/locale/vi.properties`：

```properties
mymod/effect_volume = Âm lượng hiệu ứng
mymod/enable_mod = Bật mod
```

---

## Bước 7: Kiểm tra

Khởi động trò chơi：

1. Mở menu cài đặt → tab "Mod Settings" → mod của bạn với thanh trượt và hộp kiểm.
2. Viết một tệp `.scenario` thử nghiệm với lệnh `action my_effect`.
3. Nhật ký trò chơi hiển thị `[MY_MOD] Hành động mới đã được thực thi!`.

---

## Tiếp theo là gì?

| Bạn muốn | Đi đến |
|---|---|
| Đăng ký theo dõi vào/ra cảnh | [Đăng ký Cảnh](site/docs/mods/configs/) |
| Tạo chế độ chiến đấu riêng | [Hệ thống Chiến đấu](site/docs/mods/battle/) |
| Tạo cửa sổ công cụ | [Tạo Công cụ Tùy chỉnh](site/docs/mods/tools/tutorial) |
| Viết mod bằng GDScript | [GDScript trong Mod](site/docs/mods/src/gdscript/) |
| Tất cả giao diện có sẵn | [Mã nguồn C# trong Mod](site/docs/mods/src/) |

---

## Tải xuống ví dụ hoàn chỉnh

<a href="examples/quickstart_programmer.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Tải xuống quickstart_programmer.zip
</a>

Mod tải về bằng tiếng Anh.
