# All mods in the IDE

From the game root run `ide\update_mod_projects.bat` on Windows,
`./ide/update_mod_projects.ps1` in PowerShell, or `sh ide/update_mod_projects.sh`
on Linux. Python 3 and .NET SDK 10 are required; normal NuGet restore supplies
GodotSharp and Godot source generators 4.6.3.

Open `Cthangover.All.sln` for the core, test projects and separate assemblies for
installed mods in `mods/` and `mods_private/`, including declared integrations and
test suites. You can also open a mod's `<id>.csproj` directly. Editing in the IDE
changes the actual mod source file.

Generated projects live in their mod repositories and are committed with them.
The solution belongs to the main repository. The game continues to compile
manifest-declared sources at startup and ignores these project files.

## Updating

- New `.cs` files under an existing source pattern appear through wildcards.
- Rerun after adding/removing a mod or changing its manifest, dependencies or DLLs.
- Edit the manifest rather than the generated project.

Unchanged output retains both bytes and timestamps. The script reports additions,
changes and removals; only its marked outputs are removed, and human projects are
never overwritten. Listing a fixture in the solution does not activate it in-game.

## Assembly boundaries

`sources` and `test_sources` remain separate assemblies: `<id>.csproj` and
`<id>__tests.csproj`. A nested suite depends on its host; an integration also depends
on its `with` partners. Other references follow `depends`, including its transitive
closure. The generator never infers dependencies from `using`. Missing required
dependencies fail explicitly; optional dependencies may be absent. Duplicate IDs
and cycles are rejected. Build the complete graph with `dotnet build Cthangover.All.sln`.

As in runtime collection, `src/*.cs` is recursive. Nested mods, hidden folders and
`bin/obj` do not enter the main source assembly. Mod settings are net10.0/C#12,
nullable and implicit usings disabled, with no added DEBUG/TRACE/TOOLS symbols.
Godot generators use the game's root. `Cthangover.csproj` itself is unchanged.

## Managed libraries

Existing `dll/` discovery remains supported. For libraries elsewhere, declare:

```json
"references": ["lib/**/*.dll", "vendor/SharedApi.dll"]
```

These required mod-relative patterns support `*`, `?` and `**`. Root escapes and
unmatched patterns fail; overlap is deduplicated. Nested mods keep their own files.
The IDE and runtime compiler/loader resolve the same declarations, including
archived mods. Changed DLL bytes invalidate consuming and dependent artifacts.
Native libraries continue to use `depends_native`.

`source_code/*.csproj` libraries provide source context. A source project replaces
the binary reference when both produce the same assembly. Installed releases
without `source_code` use the shipped DLL instead. A generated
`source_code/Directory.Build.props` imports common game properties and redirects
build output outside the mod.

New build artifacts live in the game root's `.ide/obj` and `.ide/bin`. Existing
`bin/obj` folders are retained and explicitly excluded from sources. `.ide/` is
ignored; repository templates also exclude `bin/obj` through `info/exclude`.

## A standalone mod without core sources

The default game root is relative to the project. Override `CthangoverRoot` when
needed. If `Cthangover.csproj` is absent, references use
`CthangoverInstalledDirectory` (default: the same root). Point it at the directory
containing `Cthangover.dll` and its shipped managed dependencies:

```text
dotnet build my_mod.csproj -p:CthangoverRoot=C:/Games/MyGame/ -p:CthangoverInstalledDirectory=C:/Games/MyGame/managed/
```

Dependency mods preferably supply their generated projects in the installed
`mods/`/`mods_private/` structure. A prebuilt dependency fallback is
`.ide/bin/<id>/<Configuration>/net10.0/<id>.dll`. Own libraries remain available
through `dll/` and `references`. IDE work needs the SDK; players' runtime mod
compilation still does not.

Test projects use the installed all-mods view, matching ModTestEngine; production project references stay restricted to depends. Legacy forwarder classes without partial retain a visible GD0001 warning through ide/godot-runtime.globalconfig, matching runtime warning policy. C# errors and missing dependency errors are not suppressed.

Project references use Private=false: API visibility and dependency builds are retained, while the core and reference pack are not copied into every mod output. Without authoring config in an installed game, the narrow GD0001 policy is generated under the project's external .ide/obj. The first full build of hundreds of projects can take considerable time; build an individual project for a local edit.
