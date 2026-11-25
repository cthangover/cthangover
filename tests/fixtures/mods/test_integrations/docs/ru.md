# Test Integrations — тест интеграций

Этот служебный мод проверяет условное подключение `test_integrations__probe`. В манифесте стоит `fixture: true`: обычные игры и `--all-mods` его не включают, пока он не добавлен явно через `--with-mods=test_integrations`.

Интеграция `probe` подключается только вместе с `quests_base`. Сюжета, контента и данных сохранения нет. Тесты проверяют наличие в игре, отсутствие в оболочке и смену набора модов в одном процессе.

```text
dotnet run --project tests/Cthangover.TestRunner -- --test integrations --test integrations_frontend
```
