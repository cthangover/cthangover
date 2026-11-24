# Resources — Pluggable Mod Infrastructure

The kernel separates discovery from file access. A scanner finds entries in a source, a loader opens an entry, and a provider exposes its files to the rest of the kernel.

```text
ModBootPipeline
  -> IModScanner: source roots and candidate entries
  -> IModLoader: open an entry
  -> IModFileProvider: read manifest.json and content
  -> ModRegistry: deduplicate IDs and select the active set
```

| Layer | Contract | Built into the kernel |
|---|---|---|
| [Scanners](scanners/) | `IModScanner` | Per-user directory and game directories |
| [Loaders](loaders/) | `IModLoader` | Directories, ZIP, and 7z |
| [Providers](providers/) | `IModFileProvider` | Directory and archive access, with a subdirectory view |
| [Discovery registry](discovery-registry/) | `ModDiscoveryRegistry` | Registration and strategy lookup |

## How precedence works

`ModDiscoveryRegistry.FindLoader` tries the last registered matching loader first. This controls how an entry is opened. If two entries have the same ID, discovery instead uses the source priority. The smaller number wins. At equal priority, the newer version wins. The first candidate stays when versions also match.

Discovery also checks each entry for a readable `manifest.json`. It can derive an ID from the entry name when a valid manifest omits `id`. Look in `MODS_REGISTRY` for invalid manifests and skipped or replaced candidates.
