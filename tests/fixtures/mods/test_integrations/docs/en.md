# Test Integrations — an integration fixture

This developer fixture checks conditional mounting of `test_integrations__probe`. Its manifest has `fixture: true`; it is excluded from ordinary games and `--all-mods` unless explicitly added with `--with-mods=test_integrations`.

The `probe` integration mounts only when `quests_base` is also loaded. It supplies no story, content or save data. The tests verify presence in a game, absence in the frontend and in-process mod-set changes.

```text
dotnet run --project tests/Cthangover.TestRunner -- --test integrations --test integrations_frontend
```
