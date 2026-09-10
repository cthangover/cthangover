# Scanners — `IModScanner`

A **scanner** knows about a *source* of mods — a directory on disk, an HTTP catalog, a Steam Workshop subscription folder, or a Git repository. Each scanner declares its root URLs via `GetRoots()` and enumerates available mods via `Discover()`.

This replaces the old hardcoded `ResolveModsRoots()` method. Instead of the kernel knowing about `user://mods/` and `res://mods/`, each scanner declares its own roots.

## Interface

```csharp
public interface IModScanner : IModDiscoveryProvider
{
    string Name { get; }                          // "UserData", "GameDir", "HTTP Catalog", ...
    bool IsCanProvide(string url);                // Accessibility guard — does the root exist?
    IEnumerable<ModSourceRoot> GetRoots();        // Roots this scanner knows about, with priorities
    IEnumerable<DiscoveredModEntry> Discover(string rootUrl);  // Enumerate mods at a root
}
```

### `IsCanProvide` — the accessibility guard

Scanners always return their potential roots from `GetRoots()`. `IsCanProvide` acts as a filter — it is called on each root before discovery, and roots where it returns `false` are silently skipped. For filesystem scanners this means `Directory.Exists(url)`; for HTTP scanners it could mean a ping or health check.

This pattern avoids calling Godot APIs outside the runtime. When `GameEnvironment.IsRuntime` is `false` (editor, tests), the scanner returns a raw Godot path like `"user://mods/"` — `Directory.Exists("user://mods/")` returns `false`, so the root is skipped without ever touching `ProjectSettings.GlobalizePath`.

### `ModSourceRoot`

Each root carries a deduplication **priority**:

```csharp
public class ModSourceRoot
{
    public string Url { get; set; }      // Filesystem path or URL
    public int Priority { get; set; }    // Lower = higher precedence in dedup
}
```

### `DiscoveredModEntry`

```csharp
public class DiscoveredModEntry
{
    public string Url { get; set; }       // Full path/URL the loader will receive
    public string EntryName { get; set; } // Folder name, zip filename, etc.
}
```

## Built-in scanners

### `UserDataModScanner` — per-user mods

Scans the user-writable mods directory (`user://mods/`). Highest priority (0) — user-installed mods override everything else.

| Context | Root returned |
|---|---|
| Runtime (`GameEnvironment.IsRuntime = true`) | Globalised `user://mods/` path |
| Editor / tests | `"user://mods/"` (filtered by `IsCanProvide`) |

```csharp
var scanner = new UserDataModScanner();
var roots = scanner.GetRoots();
// Runtime:  [ { Url = "C:/Users/.../mods", Priority = 0 } ]
// Editor:   [ { Url = "user://mods/", Priority = 0 } ]  → filtered out
```

### `GameDirModScanner` — built-in mods

Scans the game's own mods directory. Lower priority than user mods at runtime — user mods with the same ID override built-in ones.

| Context | Root returned |
|---|---|
| Runtime | Globalised `res://mods/` path, priority 1 |
| Editor / tests (`GameEnvironment.IsRuntime == false`) | `"mods"` (project-relative), priority 0 |

Note that in editor/test environments this scanner's priority (0) equals `UserDataModScanner`'s — the runtime-only precedence difference does not apply outside the Godot runtime.

## Adding a custom scanner via a mod

A mod that publishes a community catalog over HTTP:

```csharp
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using Cthangover.Core.Mods.Discovery;

namespace CommunityCatalog
{
    public class CommunityCatalogScanner : IModScanner
    {
        private const string CatalogUrl = "https://community.example.com/catalog.json";

        public string Name => "Community Catalog";

        public bool IsCanProvide(string url)
        {
            // Check if server is reachable
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var response = client.Send(new HttpRequestMessage(HttpMethod.Head, url));
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public IEnumerable<ModSourceRoot> GetRoots()
        {
            yield return new ModSourceRoot
            {
                Url = CatalogUrl,
                Priority = 10  // Lower priority than local mods
            };
        }

        public IEnumerable<DiscoveredModEntry> Discover(string rootUrl)
        {
            using var client = new HttpClient();
            var json = client.GetStringAsync(rootUrl).Result;
            var entries = JsonSerializer.Deserialize<List<CatalogEntry>>(json);

            foreach (var entry in entries)
            {
                yield return new DiscoveredModEntry
                {
                    Url = entry.DownloadUrl,
                    EntryName = entry.Name
                };
            }
        }

        private class CatalogEntry
        {
            public string Name { get; set; }
            public string DownloadUrl { get; set; }
        }
    }
}
```

Drop this class in a mod DLL with `"sources": ["src/CommunityCatalogScanner.cs"]` in `manifest.json` — the kernel discovers it automatically.

You will also need a custom loader that knows how to download and open the zip files returned by this scanner. See [Loaders](../loaders/).
