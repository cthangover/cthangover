# Сканеры — `IModScanner`

Сканеры определяют корни источников и перечисляют записи, которые могут содержать моды. `IModScanner` расширяет `IModDiscoveryProvider` с диагностическим именем `Name` и проверкой доступности `IsCanProvide(url)`.

```csharp
public interface IModScanner : IModDiscoveryProvider
{
    IEnumerable<ModSourceRoot> GetRoots();
    IEnumerable<DiscoveredModEntry> Discover(string rootUrl);
}
```

`ModSourceRoot` содержит `Url` и `Priority`. `DiscoveredModEntry` содержит `Url` для загрузчика и `EntryName` для диагностики и получения резервного ID.

## Сканеры ядра

| Сканер | Корни в рантайме | Приоритет |
|---|---|---|
| `UserDataModScanner` | Абсолютный путь `user://mods/` | 0 |
| `GameDirModScanner` | `mods/` и существующий `mods_private/` в проекте или рядом с экспортированной игрой | 1 |

Вне Godot сканер игровых каталогов возвращает относительные пути с приоритетом 0. Сканер пользовательского каталога в этом режиме возвращает строку `user://mods/` без преобразования. Проверка доступности требует существующего каталога.

## Этапы работы

`ModBootPipeline.PreparePaths` собирает корни всех зарегистрированных сканеров на потоке Godot. Затем `DiscoverAndCompile` проверяет `IsCanProvide` и вызывает `Discover` у сканера, который предоставил доступный корень. Сканеры каталогов перечисляют непосредственных потомков. Ядро пропускает скрытые записи и передаёт остальные URL [реестру загрузчиков](../discovery-registry/). Для получения доступных корней вне конвейера запуска используйте `ModRoots.ResolveModsRoots()`.
