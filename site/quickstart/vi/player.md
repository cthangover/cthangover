# Cấp 1 — Người Chơi: Vá Nhân Vật

<div class="qs-meta">
<span>Trình soạn thảo văn bản</span>
<span>5 phút</span>
<span>Không cần code</span>
</div>

Bạn sẽ cường hóa nhân vật Marao — tăng máu, tấn công và thêm khả năng "Choáng" — bằng hệ thống vá (patch). Không dòng code nào, chỉ JSON.

---

## Bước 1: Tạo thư mục mod

Mỗi mod nằm trong thư mục riêng bên trong `mods/`. Tạo:

```filestree
mods/
└── quickstart_player/
    ├── manifest.json
    └── patches/
        └── characters.json
```

---

## Bước 2: manifest.json

Mỗi mod cần một tệp kê khai. Tạo `mods/quickstart_player/manifest.json`:

```jsonc
{
  "id": "quickstart_player",        // Định danh duy nhất của mod, dùng tiếng Anh
  "name": "Cường Hóa Marao",        // Tên hiển thị trong menu quản lý mod
  "description": "Mod đầu tiên!",   // Mô tả hiển thị trong danh sách mod và catalog
  "author": "player",               // Tác giả mod (chuỗi tùy ý)
  "depends": ["core"]               // Phụ thuộc vào game lõi (không cần sắp xếp thứ tự tải mod thủ công)
}
```

Tệp kê khai tối thiểu chỉ cần `name`, nhưng nên điền cả `id`. Kernel sẽ tự động phát hiện thư mục và tải nó như một mod.

---

## Bước 3: Vá nhân vật

Tạo `mods/quickstart_player/patches/characters.json`:

```jsonc
{
  "Items": [
    {
      "Id": "Marao", // ID nhân vật cần vá (được định nghĩa trong mod core)

      "Health": 5000, // Giá trị thuộc tính mới — ghi đè giá trị gốc
      "Attack": 1200,
      "Points": 2,

      "Actions$add": ["physics/stun"] // $add thêm phần tử vào mảng
    }
  ]
}
```

### Cách hoạt động

- **`"Id": "Marao"`** — mục tiêu vá: nhân vật có định danh `Marao` (được định nghĩa trong mod `core`).
- **`"Health": 5000`**, **`"Attack": 1200`**, **`"Points": 2`** — ghi đè các trường của nhân vật. Giá trị mới thay thế giá trị cũ.
- **`"Actions$add": ["physics/stun"]`** — hậu tố `$add` nghĩa là "thêm vào mảng". Hành động `physics/stun` được thêm vào danh sách. Các hành động mặc định `physics/attack` và `physics/defence` vẫn được giữ lại.

> Tìm hiểu thêm về vá: [Patches](site/docs/mods/patches/) và [Thao tác Mảng](site/docs/mods/patches/arrays-and-identity).

---

## Bước 4: Chạy và kiểm tra

Khởi động trò chơi. Vào bất kỳ trận đấu nào — Marao giờ có:

- **5000 HP** thay vì 10
- **1200 ATK** thay vì 5
- **2 AP** thay vì 1
- Hành động thứ ba **"Choáng"** bên cạnh "Tấn công" và "Phòng thủ"

---

## Tiếp theo là gì?

| Bạn muốn | Đi đến |
|---|---|
| Tạo cảnh riêng với hội thoại | [Cấp 2 — Người Tạo Mod](#quickstart/modder) |
| Hiểu sâu hơn về hệ thống vá | [Patches](site/docs/mods/patches/) |
| Tìm hiểu về nhân vật | [Characters](site/docs/mods/characters/) |
| Viết mod C# đầu tiên | [Cấp 3 — Lập Trình Viên](#quickstart/programmer) |

---

## Tải xuống ví dụ hoàn chỉnh

<a href="examples/quickstart_player.zip" class="qs-download" download>
<svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"><path d="M12 5v14"/><path d="M5 12l7 7 7-7"/><path d="M4 22h16"/></svg>
Tải xuống quickstart_player.zip
</a>

Mod tải về bằng tiếng Anh.
