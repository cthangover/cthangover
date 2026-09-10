# Providers — `IModFileProvider`

A **provider** is the bottom layer of the resource pipeline. It gives read-only access to a single mod's files — text, binary, streams, directory listings — without the rest of the engine knowing whether those files come from a folder on disk, a `.zip` archive, or something else entirely.

## Interface

```csharp
public interface IModFileProvider : IDisposable
{
    string Mod { get; }                          // Short mod name for logging
    string RootPath { get; }                     // Path to the mod's root storage (folder or zip file)

    bool FileExists(string path);                // Check if a file exists
    IEnumerable<string> ListFiles(string directory = "");  // Shallow listing; dirs end with /
    string ReadFileText(string path);            // Read UTF-8 text, or null
    byte[] ReadFileBinary(string path);          // Read binary, or null
    Stream OpenStream(string path);              // Open a seekable stream
    string GetFileSystemPath(string path);       // Absolute path, or null (missing file / non-filesystem provider)
    string ResolveIncludePath(string includePath, string currentPath);  // Resolve relative includes
}
```

`IDisposable` is required because zip-based providers hold native OS file handles that must be released on mod unload. Folder providers implement `Dispose` as a no-op.

## Built-in providers

### `FolderModFileProvider`

Backed by a real directory on disk. This is the **only** provider type that supports `GetFileSystemPath` returning a non-null value — which means it is the only type that can serve Godot scenes via `ResourceLoader` and `PackedScene` instantiation.

```csharp
var provider = new FolderModFileProvider("mods/my_mod", "my_mod");
string text = provider.ReadFileText("manifest.json");
string fsPath = provider.GetFileSystemPath("scenes/battle.tscn");  // absolute path
```

Note that `GetFileSystemPath` returns **`null` when the file does not exist** —
not just for non-filesystem providers. Callers must handle the null return for
missing files as well.

### `ZipModFileProvider`

Backed by a `.zip` archive. On construction the entire entry table is flattened into a case-insensitive dictionary for O(1) lookups. `ListFiles` simulates directory listing by grouping flat zip entries.

```csharp
var provider = new ZipModFileProvider("mods/my_mod.zip", "my_mod");
string text = provider.ReadFileText("manifest.json");
string fsPath = provider.GetFileSystemPath("scenes/battle.tscn");  // always null
```

Limitations:
- `GetFileSystemPath` always returns `null` — Godot cannot load resources from inside a zip
- Callers that need scene instantiation must extract assets to the cache first (`ModCacheManager`)
- The zip stays open until `Dispose()` is called

## Adding a custom provider via a mod

A mod could provide a network-backed provider that streams files from a remote server:

```csharp
using System.Collections.Generic;
using System.IO;
using Cthangover.Core.Mods;

namespace MyMod
{
    public class HttpModFileProvider : IModFileProvider
    {
        private readonly HttpClient _client;
        private readonly string _baseUrl;

        public string Mod { get; }
        public string RootPath => null;

        public HttpModFileProvider(string baseUrl, string modName)
        {
            _baseUrl = baseUrl;
            Mod = modName;
            _client = new HttpClient();
        }

        public void Dispose()
        {
            _client?.Dispose();
        }

        public bool FileExists(string path)
        {
            var request = new HttpRequestMessage(HttpMethod.Head, $"{_baseUrl}/{path}");
            var response = _client.Send(request);
            return response.IsSuccessStatusCode;
        }

        public string ReadFileText(string path)
        {
            var response = _client.GetStringAsync($"{_baseUrl}/{path}").Result;
            return response;
        }

        // ... remaining methods ...
    }
}
```

To actually use a custom provider, you also need a custom `IModLoader` that creates it. See [Loaders](../loaders/).
