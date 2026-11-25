# Test Integrations — mod kiểm thử tích hợp

Mod phát triển kiểm tra việc nạp có điều kiện `test_integrations__probe`. `fixture: true` loại nó khỏi game thường và `--all-mods`, trừ khi thêm rõ bằng `--with-mods=test_integrations`.

`probe` chỉ nạp khi có `quests_base`. Không chứa truyện, nội dung hay dữ liệu lưu. Kiểm thử xác nhận có trong game, không có trong frontend và đổi bộ mod trong cùng tiến trình.

```text
dotnet run --project tests/Cthangover.TestRunner -- --test integrations --test integrations_frontend
```
