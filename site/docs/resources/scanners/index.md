# Scanners — `IModScanner`

Scanners identify source roots and enumerate possible mod entries. `IModScanner` extends `IModDiscoveryProvider`, which supplies a diagnostic `Name` and `IsCanProvide(url)` to test availability.

```csharp
public interface IModScanner : IModDiscoveryProvider
{
    IEnumerable<ModSourceRoot> GetRoots();
    IEnumerable<DiscoveredModEntry> Discover(string rootUrl);
}
```

`ModSourceRoot` contains `Url` and `Priority`. `DiscoveredModEntry` contains `Url`, passed to the loader, and `EntryName`, used for diagnostics and fallback ID selection.

## Built-in scanners

| Scanner | Runtime roots | Priority |
|---|---|---|
| `UserDataModScanner` | The absolute path for `user://mods/` | 0 |
| `GameDirModScanner` | `mods/` and, when present, `mods_private/` under the project or next to the exported game | 1 |

Outside Godot, the game-directory scanner emits relative locations with priority 0. The user scanner emits `user://mods/` literally in that context. Its availability check requires an actual directory.

## When scanners run

`ModBootPipeline.PreparePaths` collects the roots from every registered scanner on the Godot thread. `DiscoverAndCompile` then checks `IsCanProvide` and calls `Discover` on the scanner that supplied each available root. The directory scanners enumerate immediate children. Discovery skips hidden entries and hands the remaining URLs to the [loader registry](../discovery-registry/). For tools that need the currently accessible roots without running boot, use `ModRoots.ResolveModsRoots()`.
