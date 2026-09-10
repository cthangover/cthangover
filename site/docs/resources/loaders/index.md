# Loaders — `IModLoader`

A **loader** takes a single mod entry URL (returned by a scanner's `Discover()`) and creates the `IModFileProvider` that gives file access to that mod. It also provides the fallback mod ID used when `manifest.json` has no `id` field.

Loaders are matched to entries via `IsCanProvide(url)` — the first loader that says "I can handle this" (in LIFO order) gets to create the provider.

## Base interface — `IModDiscoveryProvider`

Both `IModLoader` and `IModScanner` extend a common base,
`IModDiscoveryProvider` (`Core/Mods/Discovery/IModDiscoveryProvider.cs`):

```csharp
public interface IModDiscoveryProvider
{
    string Name { get; }                // Human-readable name for log messages ("Folder", "Zip", ...)
    bool IsCanProvide(string url);      // Can this provider handle the given path or URL?
}
```

The shared contract is what lets `ModDiscoveryRegistry` discover both kinds of
providers via `Reflections.FindAndCreate` and apply the same LIFO lookup
semantics: the last-registered provider that answers `true` to
`IsCanProvide` wins. For loaders `IsCanProvide` is a type-dispatch check
(is it a folder? a zip?); for scanners it acts as an accessibility guard
(does the directory exist?).

## Interface

```csharp
public interface IModLoader : IModDiscoveryProvider
{
    string Name { get; }                                          // "Folder", "Zip", "HTTP Download", ...
    bool IsCanProvide(string url);                                // Type-dispatch: is it a folder? a zip?
    IModFileProvider CreateProvider(string url, string modName);  // Create the file provider
    string GetFallbackModId(string url, string entryName);        // ID when manifest has no id field
}
```

## Built-in loaders

### `FolderModLoader`

Claims any path that `Directory.Exists()` returns `true` for. Creates a `FolderModFileProvider` — the only provider type that supports Godot scene instantiation.

| Method | Behaviour |
|---|---|
| `IsCanProvide(url)` | `Directory.Exists(url)` |
| `CreateProvider(url, modName)` | `new FolderModFileProvider(url, modName)` |
| `GetFallbackModId(url, entryName)` | Returns `entryName` unchanged |

### `ZipModLoader`

Claims any path ending with `.zip` (case-insensitive). Creates a `ZipModFileProvider` that opens the archive and builds an O(1) entry lookup.

| Method | Behaviour |
|---|---|
| `IsCanProvide(url)` | `url.EndsWith(".zip", OrdinalIgnoreCase)` |
| `CreateProvider(url, modName)` | `new ZipModFileProvider(url, modName)` |
| `GetFallbackModId(url, entryName)` | `Path.GetFileNameWithoutExtension(entryName)` — strips the `.zip` extension |

## Loader matching order

Loaders are checked **LIFO** (last-registered first). This means:
- Built-in loaders are registered first (Folder, then Zip)
- Mod-added loaders are registered later and checked before built-ins
- A mod can override `ZipModLoader` with its own zip handler by claiming `.zip` paths first

The kernel checks for a matching loader like this:

```csharp
var loader = ModDiscoveryRegistry.FindLoader(entry.Url);  // LIFO search
if (loader == null)
    return;  // skip this entry — no loader claims it

var fileProvider = loader.CreateProvider(entry.Url, entry.EntryName);
var manifest = ReadManifest(fileProvider);
var id = manifest.Id ?? loader.GetFallbackModId(entry.Url, entry.EntryName);
```

## Adding a custom loader via a mod

A loader that downloads and opens a remote zip:

```csharp
using System;
using System.IO;
using System.Net.Http;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Discovery;
using Cthangover.Core.Mods.Providers;

namespace MyMod
{
    public class HttpZipLoader : IModLoader
    {
        public string Name => "HTTP Zip";

        public bool IsCanProvide(string url)
        {
            return url.StartsWith("https://") && url.EndsWith(".zip");
        }

        public IModFileProvider CreateProvider(string url, string modName)
        {
            using var client = new HttpClient();
            var bytes = client.GetByteArrayAsync(url).Result;

            var tempPath = Path.Combine(Path.GetTempPath(), $"{modName}.zip");
            File.WriteAllBytes(tempPath, bytes);

            return new ZipModFileProvider(tempPath, modName);
        }

        public string GetFallbackModId(string url, string entryName)
        {
            return Path.GetFileNameWithoutExtension(entryName);
        }
    }
}
```

Paired with the `CommunityCatalogScanner` from the [Scanners](../scanners/) page, you now have a complete HTTP mod pipeline: scanner discovers URLs → loader downloads and opens zips → `ZipModFileProvider` provides file access.

## Custom archive format loader

A loader for `.7z` archives could use the same pattern:

```csharp
public class SevenZipLoader : IModLoader
{
    public string Name => "7-Zip";

    public bool IsCanProvide(string url)
    {
        return url.EndsWith(".7z", StringComparison.OrdinalIgnoreCase);
    }

    public IModFileProvider CreateProvider(string url, string modName)
    {
        // Extract .7z to temp directory, then wrap with FolderModFileProvider
        var tempDir = Path.Combine(Path.GetTempPath(), modName);
        SevenZipExtractor.Extract(url, tempDir);
        return new FolderModFileProvider(tempDir, modName);
    }

    public string GetFallbackModId(string url, string entryName)
    {
        return Path.GetFileNameWithoutExtension(entryName);
    }
}
```
