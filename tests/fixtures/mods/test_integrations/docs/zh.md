# Test Integrations — 集成测试夹具

此开发测试模组检查 `test_integrations__probe` 的条件挂载。清单的 `fixture: true` 将其排除在普通游戏和 `--all-mods` 外，除非通过 `--with-mods=test_integrations` 显式加入。

`probe` 仅在同时加载 `quests_base` 时挂载。不提供剧情、内容或存档数据。测试覆盖游戏中存在、前端中不存在以及进程内模组集合切换。

```text
dotnet run --project tests/Cthangover.TestRunner -- --test integrations --test integrations_frontend
```
