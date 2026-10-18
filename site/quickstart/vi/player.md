# Cấp 1 — Người Chơi: Vá Nhân Vật

<div class="qs-meta">
<span>Trình soạn thảo văn bản</span>
<span>5 phút</span>
<span>Không cần code</span>
</div>

Bạn sẽ cường hóa nhân vật Marao — tăng máu, tăng tấn công và thêm kỹ năng "Choáng" — thông qua hệ thống bản vá. Không một dòng code nào, chỉ JSON.

---

## Bước 1: Tạo thư mục mod

Mỗi mod nằm trong thư mục riêng bên trong `mods/` (cạnh trò chơi) hoặc `user://mods/` (thư mục dữ liệu người dùng; trên Windows — `%APPDATA%\cthangover\mods`). Hãy tạo:

```filestree
mods/
└── quickstart_player/
    ├── manifest.json
    └── patches/
        └── character.json
```

---

## Bước 2: manifest.json

Mỗi mod cần một manifest. Tạo `mods/quickstart_player/manifest.json`:

```jsonc
{
  "id": "quickstart_player",          // Định danh duy nhất của mod, viết bằng tiếng Anh
  "name": "Marao Buff",               // Tên hiển thị trong menu quản lý mod
  "description": "Mod đầu tiên của tôi!", // Mô tả hiển thị trong danh sách mod và danh mục
  "author": "player",                 // Tác giả mod (chuỗi tùy ý)
  "depends": ["cthangover_game"]    // Phụ thuộc vào trò chơi chính (không cần tự sắp xếp thứ tự tải mod)
}
```

Bản thân tệp manifest là bắt buộc — mod không có nó sẽ bị bỏ qua — nhưng không trường nào thực sự bắt buộc: nếu thiếu `id`, tên thư mục sẽ được dùng làm định danh mod. Dù vậy, vẫn nên điền `id` và `name`. Trò chơi tự tìm thấy thư mục mod, nhưng mod chỉ vào trò chơi sau khi bạn kết nối nó (bước 4).

---

## Bước 3: Vá nhân vật

Tạo `mods/quickstart_player/patches/character.json`:

```jsonc
{
  "Items": [
    {
      "Id": "Marao", // ID nhân vật cần vá (được định nghĩa trong mod chính)

      "Health": 5000, // Giá trị thuộc tính mới — ghi đè giá trị gốc
      "Attack": 1200,
      "Point": 2,

      "Actions$add": ["physics/stun"] // $add thêm phần tử vào mảng
    }
  ]
}
```

### Cách hoạt động

- **`"Id": "Marao"`** — mục tiêu của bản vá: nhân vật có định danh `Marao` (được định nghĩa trong mod `cthangover_game`).
- **`"Health": 5000`**, **`"Attack": 1200`** và **`"Point": 2`** — ghi đè các trường của nhân vật. Giá trị mới thay thế giá trị cũ.
- **`"Actions$add": ["physics/stun"]`** — hậu tố `$add` nghĩa là "thêm phần tử vào mảng". Hành động `physics/stun` được thêm vào danh sách; các hành động mặc định `physics/attack` và `physics/defence` vẫn giữ nguyên. Choáng tốn 2 AP — vì vậy chúng ta cũng tăng `Point`.

> Tìm hiểu thêm về bản vá: [Patches](site/docs/mods/patches/) và [Array Operations](site/docs/mods/patches/arrays-and-identity).

---

## Bước 4: Kết nối mod với trò chơi

Trò chơi không tải mọi mod đã cài, mà chỉ tải các mod thuộc bộ sưu tập (collection) của trò chơi. Hãy thêm mod mới vào đó một lần (giao diện trò chơi có tiếng Anh và tiếng Nga):

1. Trong menu chính, mở **"Mods & Collections"** → tab **"Collections"**.
2. Trên thẻ trò chơi **Cthangover**, nhấn **"Edit scheme"**.
3. Chọn bất kỳ cơ chế nào trên sơ đồ (ví dụ **"Story"**) và nhấn **"Add mod"**.
4. Tìm mod của bạn trong danh sách (theo tên trong `manifest.json`) và nhấn **"Add"**.
5. Nhấn **"Save"** và xác nhận. Thay đổi có hiệu lực ở lần khởi động trò chơi tiếp theo.

> **Dành cho nhà phát triển.** Khi chạy từ mã nguồn, bạn có thể kết nối mod mà không cần menu, bằng cờ: `godot --path . -- --game=cthangover --new --with-mods=quickstart_player`.

---

## Bước 5: Chạy và kiểm tra

Khởi động lại trò chơi và bắt đầu ván mới. Vào bất kỳ trận chiến nào — giờ Marao có:

- **5000 HP** thay vì 16
- **1200 ATK** thay vì 5
- **2 AP** thay vì 1
- Hành động thứ ba **"Stun"** (Choáng) bên cạnh "Basic attack" và "Defence"

---

## Tiếp theo là gì?

| Bạn muốn | Đi đến |
|---|---|
| Tạo cảnh riêng có hội thoại | [Cấp 2 — Người Tạo Mod](#quickstart/modder) |
| Hiểu sâu hơn hệ thống bản vá | [Patches](site/docs/mods/patches/) |
| Tìm hiểu về nhân vật | [Characters](site/docs/mods/characters/) |
| Viết mod C# đầu tiên | [Cấp 3 — Lập Trình Viên](#quickstart/programmer) |

---

## Tải xuống ví dụ hoàn chỉnh

<a href="examples/quickstart_player.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Tải xuống quickstart_player.zip
</a>

Mod tải về bằng tiếng Anh. Để cài đặt, hãy giải nén tệp vào thư mục `mods/quickstart_player/` của trò chơi — hoặc đặt chính tệp `.zip` vào `user://mods/`: trò chơi đọc mod trực tiếp từ tệp nén, và mod trong `user://mods/` được ưu tiên hơn mod tích hợp có cùng id. Sau khi cài, hãy kết nối mod với trò chơi (bước 4).
