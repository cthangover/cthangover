# Test Integrations — 統合のテスト用 MOD

`test_integrations__probe` の条件付きマウントを確認する開発用 MOD です。`fixture: true` のため通常のゲームや `--all-mods` から除外され、`--with-mods=test_integrations` で明示追加します。

`probe` は `quests_base` がある場合だけマウントされます。物語、コンテンツ、セーブデータはありません。ゲーム内での存在、フロントエンドでの不在、同一プロセス内の MOD 切替を確認します。

```text
dotnet run --project tests/Cthangover.TestRunner -- --test integrations --test integrations_frontend
```
