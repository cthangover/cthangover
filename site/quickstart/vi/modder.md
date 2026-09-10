# Cấp 2 — Người Tạo Mod: Cảnh DSL

<div class="qs-meta">
<span>JSON + DSL</span>
<span>15 phút</span>
<span>Không cần code</span>
</div>

Bạn sẽ tạo một mod với cảnh riêng — một căn phòng có hội thoại, lựa chọn và chuyển cảnh. Đồng thời bạn sẽ thêm tùy chọn "Đến quán rượu" vào cảnh bếp hiện có mà không chạm vào tệp gốc.

---

## Bước 1: Cấu trúc mod

Tạo thư mục và các tệp cơ sở:

```filestree
mods/
└── quickstart_modder/
    ├── manifest.json
    ├── scenes/
    │   └── my_alko_bar.json              # Địa điểm mới của chúng ta — quán rượu!
    ├── scenarios/
    │   ├── my_alko_bar.scenario          # Kịch bản trong quán rượu, cho phép người chơi tương tác với địa điểm mới
    │   └── town_entry_override.scenario  # Ghi đè cảnh thị trấn để thêm lối vào địa điểm mới
    ├── backgrounds/
    │   └── my_alko_bar/
    │       └── bg.png                    # Hình ảnh nội thất quán rượu
    ├── backgrounds/
    │   └── sounds/
    │       └── alko_bar_ambient.ogg      # Âm thanh ồn ào của quán rượu
    └── locale/
        └── ru.properties                 # Bản địa hóa tiếng Nga
```

---

## Bước 2: manifest.json

`mods/quickstart_modder/manifest.json`：

```jsonc
{
  "id": "alko_bar",                                   // ID mod
  "name": "Quán Rượu Của Tôi",                        // Tên mod hiển thị trong menu mod
  "description": "Thêm cảnh quán rượu vào thị trấn",  // Mô tả mod hiển thị trong menu mod
  "author": "modder",                                 // Tác giả mod
  "depends": ["core"]                                 // Phụ thuộc vào game lõi (không cần sắp xếp thứ tự tải mod thủ công)
}
```

---

## Bước 3: Định nghĩa cảnh

`mods/quickstart_modder/scenes/my_alko_bar.json`：

```jsonc
{
  "name": "my_alko_bar",                              // Tạo định danh cảnh duy nhất mới (dùng trong switch_scene), đây sẽ là cảnh mới.
  "defaultBackground": "my_alko_bar/bg",              // Đường dẫn đến nền PNG trong backgrounds/ (không có phần mở rộng)
  "defaultAmbient": "locations/alkobar/bar_ambient",  // ID âm thanh nền cho địa điểm này (không có phần mở rộng), vd: "tiếng ồn quán rượu" phát khi vào cảnh
  "defaultScenario": "scenarios/my_alko_bar.scenario" // Kịch bản mặc định sẽ chạy trong cảnh này nếu không có gì khác được chỉ định
}
```

| Trường | Mô tả |
|---|---|
| `name` | ID cảnh duy nhất. Dùng trong `switch_scene` |
| `defaultBackground` | Đường dẫn đến nền trong `backgrounds/` (không có phần mở rộng) |
| `defaultAmbient` | Âm thanh nền. Chuỗi rỗng — không có âm thanh |
| `defaultScenario` | Đường dẫn đến tệp `.scenario` chạy khi vào cảnh |

---

## Bước 4: Hình nền

Đặt hình ảnh vào `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`.

Bất kỳ ảnh PNG nào cũng được. Trò chơi hỗ trợ ánh sáng dựa trên shader và thời gian trong ngày — nền sẽ được chiếu sáng tự động.

> Nếu không có ảnh, tạm thời sao chép `mods/core/backgrounds/home/kitchen.png` → `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`.

---

## Bước 5: Kịch bản DSL

`mods/quickstart_modder/scenarios/my_alko_bar.scenario`：

```scenario
scene: my_alko_bar
priority: 10
---
text "Bạn bước vào một quán rượu vắng khách."
text "Một người pha chế buồn chán đứng sau quầy."

select "[Bạn sẽ làm gì?]"
option "Đến quầy" -> :bar
option "Rời đi" -> :leave

:bar
text "Người pha chế gật đầu chào bạn."
text "— Dùng gì ạ?"
end

:leave
switch_scene town_entry
end
```

### Điều gì đang xảy ra ở đây

- **`text`** — hiển thị một dòng hội thoại. Tham số `key=` là khóa bản địa hóa.
- **`select`** — mở menu lựa chọn. Văn bản trong ngoặc kép là lời nhắc.
- **`option`** — một lựa chọn. `-> :label` — nơi để nhảy đến khi được chọn.
- **`:bar`**, **`:leave`** — nhãn cho các bước nhảy.
- **`switch_scene`** — di chuyển người chơi đến cảnh khác.
- **`end`** — kết thúc kịch bản (người chơi ở lại cảnh).

---

## Bước 6: Mở rộng thị trấn (ưu tiên cảnh)

Bây giờ thêm tùy chọn "Đến quán rượu" vào cảnh thị trấn, **không chạm vào** tệp `core` gốc.

`mods/quickstart_modder/scenarios/town_entry_override.scenario`：

```scenario
scene: town_entry
priority: 1
---
select "Thị trấn hôm nay có mùi khác lạ..."
option "Về nhà" -> :home
option "Đến quán rượu" -> :alko_bar

:home
switch_scene home_outside
end

:alko_bar
switch_scene my_alko_bar
end
```

### Tại sao điều này hoạt động

Cảnh `town_entry` giờ có hai kịch bản:
- `priority: 10` — mặc định (từ mod `core`)
- `priority: 1` — của chúng ta (từ `quickstart_modder`)

Engine chạy kịch bản có `priority` **thấp nhất**. `priority: 1` của chúng ta thắng — người chơi thấy tùy chọn mới.

> Tìm hiểu thêm: [Hệ thống Ưu tiên Cảnh](site/docs/mods/scenes/).

---

## Bước 7: Kiểm tra

Khởi động trò chơi:

1. Vào thị trấn — bạn thấy tùy chọn "Đến quán rượu".
2. Nhấp vào — bạn đang ở cảnh `my_alko_bar` với hội thoại và lựa chọn.
3. Chọn "Rời đi" — bạn quay lại thị trấn.

---

## Tiếp theo là gì?

| Bạn muốn | Đi đến |
|---|---|
| Thành thạo tất cả lệnh DSL | [Tham khảo Lệnh DSL](site/docs/mods/scenes/scenarios/dsl/) |
| Thêm đối tượng tương tác | [Đối tượng Tương tác](site/docs/mods/interactives/) |
| Viết mod C# | [Cấp 3 — Lập Trình Viên](#quickstart/programmer) |

---

## Tải xuống ví dụ hoàn chỉnh

<a href="examples/quickstart_modder.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Tải xuống quickstart_modder.zip
</a>

Mod tải về bằng tiếng Anh.
