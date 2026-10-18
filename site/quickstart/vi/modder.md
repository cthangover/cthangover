# Cấp 2 — Người Tạo Mod: Cảnh DSL

<div class="qs-meta">
<span>JSON + DSL</span>
<span>15 phút</span>
<span>Không cần code</span>
</div>

Bạn sẽ tạo một mod có cảnh riêng — một quán bar với hội thoại, lựa chọn và chuyển cảnh. Đồng thời bạn sẽ thêm lối vào quán bar vào cảnh thị trấn có sẵn mà không động đến các tệp gốc.

---

## Bước 1: Cấu trúc mod

Tạo thư mục và các tệp cơ bản:

```filestree
mods/
└── quickstart_modder/
    ├── manifest.json
    ├── scenes/
    │   └── my_alko_bar.json              # Địa điểm mới của chúng ta — quán rượu!
    ├── scenarios/
    │   ├── my_alko_bar.scenario          # Kịch bản bên trong quán, cho người chơi tương tác với địa điểm mới
    │   └── town_entry_override.scenario  # Mở rộng cảnh thị trấn để thêm lối sang địa điểm mới
    ├── backgrounds/
    │   └── my_alko_bar/
    │       └── bg.png                    # Ảnh nội thất quán bar
    └── sounds/
        └── locations/
            └── alkobar/
                └── bar_ambient.ogg       # Âm thanh nền ồn ào của quán
```

---

## Bước 2: manifest.json

`mods/quickstart_modder/manifest.json`:

```jsonc
{
  "id": "quickstart_modder",                     // ID mod
  "name": "Quán bar của tôi",                     // Tên mod hiển thị trong menu mod
  "description": "Thêm cảnh quán bar vào thị trấn", // Mô tả mod hiển thị trong menu mod
  "author": "modder",                            // Tác giả mod
  "depends": ["cthangover_game"]               // Phụ thuộc vào trò chơi chính (không cần tự sắp xếp thứ tự tải mod)
}
```

---

## Bước 3: Định nghĩa cảnh

`mods/quickstart_modder/scenes/my_alko_bar.json`:

```jsonc
{
  "name": "my_alko_bar",                              // Định danh cảnh mới, duy nhất (dùng trong switch_scene), đây là cảnh mới của chúng ta
  "defaultBackground": "my_alko_bar/bg",              // Đường dẫn tới ảnh nền PNG trong backgrounds/ (không có phần mở rộng)
  "defaultAmbient": "locations/alkobar/bar_ambient",  // ID âm thanh nền của địa điểm (đường dẫn trong sounds/, không có phần mở rộng), ví dụ "tiếng ồn quán bar" phát khi vào cảnh
  "defaultScenario": "scenarios/my_alko_bar.scenario" // Kịch bản mặc định chạy trong cảnh này nếu không có gì khác
}
```

| Trường | Ý nghĩa |
|---|---|
| `name` | ID duy nhất của cảnh. Dùng trong `switch_scene` |
| `defaultBackground` | Đường dẫn tới ảnh nền trong `backgrounds/` (không có phần mở rộng) |
| `defaultAmbient` | Âm thanh nền: đường dẫn trong `sounds/`, không có phần mở rộng. Chuỗi rỗng — không có âm thanh |
| `defaultScenario` | Đường dẫn tới tệp `.scenario` chạy khi vào cảnh |

---

## Bước 4: Hình nền và âm thanh

Đặt ảnh vào `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`, và âm thanh nền vào `mods/quickstart_modder/sounds/locations/alkobar/bar_ambient.ogg`.

Ảnh PNG nào cũng được. Trò chơi hỗ trợ chiếu sáng bằng shader và thời gian trong ngày — hình nền sẽ được chiếu sáng tự động.

> Nếu chưa có ảnh riêng, hãy tạm sao chép `mods/cthangover_game/backgrounds/home/kitchen.png` → `mods/quickstart_modder/backgrounds/my_alko_bar/bg.png`. Cũng có thể bỏ âm thanh: đặt `"defaultAmbient": ""`.

---

## Bước 5: Kịch bản DSL

`mods/quickstart_modder/scenarios/my_alko_bar.scenario`:

```scenario
scene: my_alko_bar
priority: 10
---
text "Bạn bước vào một quán bar vắng nửa."
text "Sau quầy, người pha chế đang buồn chán."

:menu
select "[Bạn sẽ làm gì?]"
option "Đến quầy" -> :bar
option "Rời đi" -> :leave

:bar
text "Người pha chế gật đầu với bạn."
text "— Quý khách dùng gì?"
goto -> :menu

:leave
switch_scene town_entry
end
```

### Điều gì đang xảy ra ở đây

- **`scene:`** / **`priority:`** — phần đầu: kịch bản thuộc cảnh nào và chạy theo thứ tự nào. Phần đầu được tách khỏi thân bằng dòng `---`.
- **`text`** — hiển thị một dòng hội thoại. Tham số `key=` là khóa bản địa hóa (ở đây không cần: nếu không có, chính văn bản sẽ được hiển thị).
- **`select`** — mở menu lựa chọn. Văn bản trong ngoặc kép là lời nhắc.
- **`option`** — một lựa chọn. `-> :label` — nơi nhảy tới khi chọn.
- **`:menu`**, **`:bar`**, **`:leave`** — các nhãn để nhảy tới.
- **`goto -> :menu`** — nhảy vô điều kiện: sau khi nói chuyện với người pha chế, menu lại mở ra.
- **`switch_scene`** — chuyển người chơi sang cảnh khác.
- **`end`** — kết thúc kịch bản.

---

## Bước 6: Mở rộng thị trấn (ưu tiên cảnh)

Giờ hãy thêm lối vào quán bar vào cảnh thị trấn, **không động đến** các tệp gốc của `cthangover_game`.

`mods/quickstart_modder/scenarios/town_entry_override.scenario`:

```scenario
scene: town_entry
priority: 1
---
select "Tiếng nhạc vọng ra từ cánh cửa quán bar khép hờ..."
option "Vào quán bar" -> :alko_bar
option "Đi ngang qua" -> :pass

:alko_bar
switch_scene my_alko_bar
end

:pass
end
```

### Tại sao điều này hoạt động

Cảnh `town_entry` giờ có nhiều kịch bản:

| priority | Kịch bản | Mod |
|---|---|---|
| `1` | `town_entry_override` của chúng ta | `quickstart_modder` |
| `10`–`30` | cuộc gặp Murakami (chỉ khi `condition:` của chúng còn đúng) | `cthangover_murakami` |
| `40` | menu thị trấn tiêu chuẩn | `cthangover_game` |

Khi vào một cảnh, engine chạy kịch bản hợp lệ đầu tiên theo thứ tự `priority` tăng dần — bắt đầu từ giá trị **nhỏ nhất**. Khi một kịch bản kết thúc mà không đổi cảnh, kịch bản tiếp theo sẽ bắt đầu. Vì vậy:

- "Vào quán bar" — `switch_scene` đưa người chơi vào quán;
- "Đi ngang qua" — kịch bản của chúng ta chỉ đơn giản kết thúc (`end`), và mọi thứ tiếp tục như trong trò chơi gốc: cảnh cốt truyện của Murakami hoặc menu thị trấn tiêu chuẩn.

Bằng cách này mod thêm nội dung mới mà không phá hỏng gì. Nếu kịch bản của chúng ta tự vẽ menu thị trấn, nó sẽ che mất cả cốt truyện lẫn các mod khác.

> Tìm hiểu thêm: [Scene Priority System](site/docs/mods/scenes/).

---

## Bước 7: Kết nối mod với trò chơi

Trò chơi không tải mọi mod đã cài, mà chỉ tải các mod thuộc bộ sưu tập của trò chơi. Hãy thêm mod của bạn (giao diện trò chơi có tiếng Anh và tiếng Nga):

1. Trong menu chính, mở **"Mods & Collections"** → tab **"Collections"**.
2. Trên thẻ trò chơi **Cthangover**, nhấn **"Edit scheme"**.
3. Chọn bất kỳ cơ chế nào trên sơ đồ (ví dụ **"Story"**) và nhấn **"Add mod"**.
4. Tìm mod của bạn trong danh sách (theo tên trong `manifest.json`) và nhấn **"Add"**.
5. Nhấn **"Save"** và xác nhận. Thay đổi có hiệu lực ở lần khởi động trò chơi tiếp theo.

---

## Bước 8: Kiểm tra

Khởi động lại trò chơi:

1. Vào thị trấn — điều đầu tiên bạn thấy là lựa chọn "Vào quán bar" / "Đi ngang qua".
2. Nhấn "Vào quán bar" — bạn ở trong cảnh `my_alko_bar` của mình với hình nền, tiếng ồn quán bar, hội thoại và lựa chọn.
3. Chọn "Rời đi" — bạn quay lại thị trấn.
4. Chọn "Đi ngang qua" — trò chơi tiếp tục như bình thường.

> **Dành cho nhà phát triển.** Từ mã nguồn, bạn có thể mở cảnh ngay mà không cần chơi qua phần mở đầu: `godot --path . -- --game=cthangover --new --with-mods=quickstart_modder --scene=town_entry`.

---

## Tiếp theo là gì?

| Bạn muốn | Đi đến |
|---|---|
| Nắm vững mọi lệnh DSL | [DSL Command Reference](site/docs/mods/scenes/scenarios/dsl/) |
| Thêm đối tượng tương tác | [Interactive Objects](site/docs/mods/interactives/) |
| Viết mod C# | [Cấp 3 — Lập Trình Viên](#quickstart/programmer) |

---

## Tải xuống ví dụ hoàn chỉnh

<a href="examples/quickstart_modder.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Tải xuống quickstart_modder.zip
</a>

Lời thoại trong mod tải về bằng tiếng Nga. Giải nén tệp vào `mods/quickstart_modder/` hoặc đặt tệp `.zip` vào `user://mods/`, sau đó kết nối mod với trò chơi (bước 7).
