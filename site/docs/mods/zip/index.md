# Mod Packaging — .zip Distribution

Mods can be distributed as `.zip` archives instead of loose folders. The kernel loads them identically — zip is just a transport format.

## When to use .zip vs folder

| Format | Use case |
|---|---|
| **Folder** | Local development. Instant iteration — edit files, reload game |
| **.zip** | Distribution. Share with players, upload to mod platforms, versioned releases |

## Directory structure inside the zip

The zip must have the same structure as a loose mod folder:

```filestree
my_mod.zip/
├── manifest.json
├── src/
│   └── MyCode.cs
├── scenarios/
│   └── my_scene.scenario
├── items/
│   └── rations.json
├── backgrounds/
│   └── my_bg.png
├── locale/
│   └── en.properties
└── ...                     # any other assets
```

The root of the zip IS the mod root — no extra wrapper folder.

## How the kernel loads zipped mods

1. On startup, the registered mod scanners (`UserDataModScanner` and `GameDirModScanner`) enumerate filesystem entries in their respective `mods/` directories — both folders and `.zip` files are returned as `DiscoveredModEntry` items
2. The `ZipModLoader` claims any entry path ending with `.zip` and creates a `ZipModFileProvider` — the archive is opened directly with `ZipFile.OpenRead`, building an O(1) entry lookup dictionary in memory without extracting files to disk
3. The mod is loaded as if it were a folder — `manifest.json` is read from inside the zip, sources compiled, assets discovered
4. Zip mods have the same dependency resolution and lifecycle as folder mods; deduplication priority is controlled by the scanner's `ModSourceRoot.Priority`, not by format type

See [Resources — Loaders](../resources/loaders/) for details on how `ZipModLoader` and custom loaders work.

## Creating a .zip for distribution

From the mod's parent directory:

```bash
# From mods/
zip -r my_mod_v1.0.0.zip my_mod/
```

Or using the Python build script (add this to your mod's tooling):

```python
import shutil
import json

def pack_mod(mod_path, output_path):
    with open(f"{mod_path}/manifest.json") as f:
        manifest = json.load(f)

    name = manifest.get("name", mod_path)
    version = manifest.get("version", "0.0.0")
    archive = f"{output_path}/{name}_v{version}"

    shutil.make_archive(archive, "zip", mod_path)
    print(f"Packed: {archive}.zip")
```

## Testing a zipped mod

1. Place the `.zip` in `mods/`
2. Remove the source folder (or keep both — if both a folder and a zip share the same mod ID, only one loads: a higher version always wins regardless of enumeration order; when versions are equal the first-encountered entry wins within the same root priority, and a lower-priority root always loses)
3. Launch the game — the mod loads from the zip
4. Check the game log for any errors

## Mod versioning

Include a `version` field in `manifest.json` for proper version tracking:

```jsonc
{
  "name": "MyMod",                                         // Human-readable mod name
  "version": "1.2.0",                                      // Semver version for tracking and compatibility
  "author": "you",                                         // Author credit
  "description": "..."                                     // Short description of the mod
}
```

The kernel **does** enforce version deduplication — `ModRegistry.RegisterCandidates()` keys candidates by mod `Id`, so only **one** version of a given mod ever loads:

- Within the same root priority: a candidate with a **higher version replaces** the already-registered one (logged as a warning); an equal or lower version is **skipped with a warning** — for equal versions, first-encountered wins.
- A candidate from a **lower-priority root never replaces** one from a higher-priority source, regardless of version.

Use version numbers for player information and mod platform compatibility checks.

## Zip mod limitations

- **No hot-reload**: Editing files inside a zip requires repacking. Use folders during development
- **No filesystem paths**: `GetFileSystemPath()` always returns `null` for zip mods — Godot's `ResourceLoader` cannot load scenes or shaders directly from within a zip. Assets that require filesystem access (shaders, scenes) are extracted to the mod cache on demand
- **File size**: No hard limit on zip size, but the entry lookup dictionary grows linearly with the number of files — very large archives (>10,000 entries) may increase the initial dictionary build time

## Existing test zips

The project includes test zip mods in `mods/` — these demonstrate the zip loading system:

```filestree
mods/
├── test_mod.zip
├── test_mod_a.zip
└── test_mod_b.zip
```

These are minimal mods used in automated tests to verify zip loading works correctly.
