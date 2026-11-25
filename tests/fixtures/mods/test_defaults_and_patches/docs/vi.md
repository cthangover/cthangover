# Test Defaults and Patches — fixture thử nghiệm cho bản vá thuộc tính

Mod này không phải nội dung. Nó là một **fixture thử nghiệm**: một bộ nhỏ bản vá thuộc tính mà bài kiểm tra `property_patches` nạp để kiểm tra rằng các bản vá lên nhân vật và vật phẩm của mod khác được ghép đúng như game hứa. Nó không bao giờ thuộc về một game nào và cũng không nhằm để người chơi bật.

## Nó vá những gì

Một bản vá thuộc tính là tệp `patches/<kind>.json`. Mỗi mục nêu tên một thực thể theo `Id` và liệt kê các thuộc tính cần thêm hay sửa; khi `Id` là `*` thì đó là **ký tự đại diện**, chạm tới mọi thực thể của loại đó. Fixture có một bản vá có đích và một bản vá ký tự đại diện cho mỗi loại:

```json
{
  "Items": [
    { "Id": "Marao", "TestPatched": 42, "TestDefault": 100 },
    { "Id": "*", "TestDefault": -20, "TestGlobalPatch": 1 }
  ]
}
```

```json
{
  "Items": [
    { "Id": "food/ration", "Durability": 99, "SpecialProp": true },
    { "Id": "*", "Durability": 50, "GlobalItemProp": "patched" }
  ]
}
```

Các quy tắc mà những bản vá này kiểm tra:

- bản vá **có đích** thắng ký tự đại diện: Marao nhận `TestDefault` 100, chứ không phải -20;
- **ký tự đại diện** chạm tới mọi thực thể còn lại: nhân vật nào khác cũng nhận `TestDefault` -20, mọi vật phẩm nhận `Durability` 50;
- khóa nào chỉ một bên đặt thì đều được giữ: Marao còn nhận thêm `TestGlobalPatch` 1, khẩu phần nhận `GlobalItemProp`;
- khóa có đích không bao giờ lan sang thực thể khác;
- các trường có kiểu của chính thực thể, như tên khẩu phần, sống sót qua việc ghép.

## Chạy bài kiểm tra

```text
godot --path . res://scenes/menu/launcher.tscn -- --with-mods=test_defaults_and_patches --itest=property_patches
dotnet run --project tests/Cthangover.TestRunner -- --test property_patches
```

Fixture chỉ được nạp vì `--with-mods=` nêu tên nó. Kết quả của nó trong một lần chạy thật:

```text
[PASS] Marao: targeted value: TestPatched=42
[PASS] Marao: targeted beats wildcard: TestDefault=100
[PASS] Marao: wildcard-only key: TestGlobalPatch=1
[PASS] werewolf_girl_1: wildcard value: TestDefault=-20
[PASS] werewolf_girl_1: no targeted key
[PASS] ration: targeted beats wildcard: Durability=99
[PASS] ration: targeted-only key: SpecialProp=true
[PASS] ration: wildcard-only key: GlobalItemProp=patched
[PASS] ration: typed field survives the merge
[PASS] wolf_meat: wildcard value: Durability=50
[TEST_RESULT] name=property_patches passed=14 failed=0
```

Bài kiểm tra nằm ngay trong mod (`src/PropertyPatchesTest.cs`) và chỉ hoạt động khi có `--itest=property_patches`.

## Vì sao nó đứng ngoài game

Manifest của nó ghi `"fixture": true`. Một fixture không bao giờ thuộc `--all-mods` hay bộ mod của game; nó chỉ được nạp khi `--with-mods=` nêu tên, nên các bản vá của nó không thể lọt vào bài kiểm tra khác hay vào lượt chơi. Nếu bật trong một game thật, nó sẽ thêm thuộc tính thử nghiệm cho từng nhân vật và từng vật phẩm của Cthangover — bản vá ký tự đại diện chạm tới mọi thứ. Nó phụ thuộc vào các mod Cthangover mà nó vá (`cthangover_game`, `cthangover_murakami`, `cooking`).

Mod này cũng là nơi an toàn cho các bài kiểm tra công cụ ghi vào: bài `tools_windows` tạo một cảnh trong đó qua trình soạn thảo cảnh rồi xóa đi.
