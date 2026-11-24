# Loaders — `IModLoader`

A loader opens a single entry discovered by a scanner. `IsCanProvide(url)` decides whether the strategy recognizes that entry. `CreateProvider` opens its storage and returns an [`IModFileProvider`](../providers/). The discovery pipeline owns that provider and disposes it if the candidate is skipped or replaced.

```csharp
public interface IModLoader : IModDiscoveryProvider
{
    IModFileProvider CreateProvider(string url, string modName);
    string GetFallbackModId(string url, string entryName);
}
```

| Built-in loader | Entry recognized | Fallback ID |
|---|---|---|
| `FolderModLoader` | Existing directory | Directory name |
| `ZipModLoader` | Case-insensitive `.zip` suffix outside existing directories | File name without extension |
| `SevenZipModLoader` | Case-insensitive `.7z` suffix outside existing directories | File name without extension |

The fallback ID applies only when a readable `manifest.json` omits its `id`. A directory named `example.zip` goes through the directory loader. ZIP and 7z loaders can obtain remembered credentials from the kernel's per-user archive password store (`user://archive_passwords.json`). Archive support is built into the runtime and requires no separate 7-Zip installation.

[Discovery registry](../discovery-registry/) describes how the kernel chooses a loader when several strategies match.
