# Providers — `IModFileProvider`

The provider exposes a single storage root through mod-relative paths. The same interface serves manifest readers, the compiler, and resource loaders. The kernel accepts folder providers and archive providers for ZIP and 7z. `SubdirModFileProvider` restricts access to a subtree of an existing provider.

| Member | Contract |
|---|---|
| `Mod`, `RootPath` | Storage identifier and backing filesystem path, when one exists |
| `FileExists(path)` | Test for a file at a relative path |
| `ListFiles(directory)` | Enumerate immediate children. Directory names end in `/`, and hidden entries are omitted |
| `ReadFileText(path)`, `ReadFileBinary(path)` | Read the whole file as UTF-8 or bytes. Missing files return `null` |
| `OpenStream(path)` | Open a caller-owned stream. An archive stream may only support forward reads |
| `GetFileSystemPath(path)` | Return a physical path when one is available. Archive providers return `null` |
| `ResolveIncludePath(includePath, currentPath)` | Resolve an include relative to its source file |
| `Dispose()` | Release the provider and any open backing archive |

Close streams before the provider is released. For large files, use `OpenStream` when the consumer supports streaming. When processing a group of archived files, the kernel's archive providers also implement `IModFileSequence.VisitFiles` for sequential reads. A consumer that requires a physical filesystem path must account for archive entries.
