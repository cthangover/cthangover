using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Scenes.Patching;
using Cthangover.Core.Scenes.TscnBuild;
using Cthangover.Core.Scenes.TscnParsing;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Mod-integrated resolver for runtime TSCN scene loading.
    /// Collects .tscn files from all mods' "scenes/" directories into a
    /// deduplicated index (later mods override earlier ones), then resolves
    /// scenes by mod-relative path without requiring callers to know which
    /// mod provides the file.
    /// <para/>
    /// Connects <see cref="TscnParser"/> and <see cref="SceneBuilder"/> to
    /// the mod system: reads source text through <see cref="IModFileProvider"/>,
    /// handles recursive PackedScene instantiation with cycle detection,
    /// and caches both parsed scenes and built node trees.
    /// <para/>
    /// Registered as <c>"tscn_scenes"</c> resolver in <see cref="ModManager"/>.
    /// Cleared on mod reload (<see cref="ClearCache"/>).
    /// </summary>
    public class TscnSceneResolver : ModAssetResolver
    {
        /// <inheritdoc />
        public override string Id => "tscn_scenes";

        private Dictionary<string, FileEntry> _sceneIndex;
        private readonly Dictionary<string, ParsedScene> _parsedCache = new();
        private readonly Dictionary<string, Node> _builtCache = new();
        private readonly TscnParser _parser = new();
        private readonly HashSet<string> _buildingPaths = new();
        private ParsedScene _resolvingScene;

        /// <summary>
        /// Collects all .tscn scene files from every mod's "scenes/" directory.
        /// Builds a flat index keyed by mod-relative path (e.g. "scenes/main_menu.tscn").
        /// Later mods in load order override earlier ones with the same path.
        /// The result is cached until <see cref="ClearCache"/> is called.
        /// </summary>
        public Dictionary<string, FileEntry> Collect()
        {
            Context.Initialize();

            if (_sceneIndex != null)
                return _sceneIndex;

            var result = new Dictionary<string, FileEntry>();
            var fileListResolver = Context.GetResolver<FileListResolver>();
            if (fileListResolver == null)
            {
                _sceneIndex = result;
                return result;
            }

            var files = fileListResolver.Collect("scenes");
            foreach (var kvp in files)
            {
                var ext = Path.GetExtension(kvp.Key);
                if (ext.Equals(".tscn", StringComparison.OrdinalIgnoreCase))
                    result[kvp.Value.FullPath] = kvp.Value;
            }

            _sceneIndex = result;
            GameLogger.Log("TSCN", $"Collect → {result.Count} scene(s)");

            return result;
        }

        /// <summary>
        /// Loads, parses, and builds a .tscn scene from the mod system.
        /// The scene is resolved by its mod-relative path (e.g. "scenes/main_menu.tscn")
        /// — the resolver determines which mod provides the file, honouring load-order
        /// overrides. Built nodes are cached and duplicated on each hit so the caller
        /// receives a fresh instance every time.
        /// </summary>
        /// <param name="scenePath">
        /// Mod-relative path to the .tscn file, may optionally start with "res://" (stripped internally).
        /// </param>
        public Node LoadAndBuild(string scenePath)
        {
            scenePath = NormalizeResourcePath(scenePath);

            var index = Collect();
            string modId = null;

            if (index.TryGetValue(scenePath, out var entry))
                modId = entry.ModId;
            else
            {
                var provider = ResolveProviderForPath(scenePath, out modId);
                if (provider == null)
                {
                    if (HasScenePatches(scenePath))
                        return BuildNativePatched(scenePath);

                    var native = LoadNative(scenePath);
                    if (native != null)
                    {
                        GameLogger.Log("TSCN", $"TscnSceneResolver.LoadAndBuild: native fallback '{scenePath}' → root='{native.Name}' ({native.GetType().Name})");
                        return native;
                    }

                    GameLogger.Log("TSCN", $"TscnSceneResolver.LoadAndBuild: scene '{scenePath}' not found in any mod", LogLevel.Error);
                    return null;
                }
            }

            var cacheKey = scenePath;

            if (_builtCache.TryGetValue(cacheKey, out var cached))
            {
                GameLogger.Log("TSCN", $"TscnSceneResolver.LoadAndBuild: HIT cached '{cacheKey}'", LogLevel.Debug);
                return DuplicateBuilt(cached);
            }

            GameLogger.Log("TSCN", $"TscnSceneResolver.LoadAndBuild: loading '{cacheKey}' from mod '{modId}'...");

            if (!_buildingPaths.Add(cacheKey))
            {
                GameLogger.Log("TSCN", $"TscnSceneResolver: circular reference detected for '{cacheKey}'", LogLevel.Error);
                return null;
            }

            try
            {
                var parsed = LoadAndParse(scenePath, modId);
                if (parsed == null)
                {
                    GameLogger.Log("TSCN", $"TscnSceneResolver.LoadAndBuild: FAILED parse for '{cacheKey}'", LogLevel.Error);
                    return null;
                }

                var previousScene = _resolvingScene;
                _resolvingScene = parsed;
                var root = new SceneBuilder().Build(
                    parsed,
                    InstanceLoader,
                    ResolveExtResource);
                _resolvingScene = previousScene;

                if (root != null)
                {
                    _builtCache[cacheKey] = root;
                    GameLogger.Log("TSCN", $"TscnSceneResolver.LoadAndBuild: OK '{cacheKey}' → root='{root.Name}' ({root.GetType().Name})");
                    return DuplicateBuilt(root);
                }

                return null;
            }
            finally
            {
                _buildingPaths.Remove(cacheKey);
            }
        }

        private Node DuplicateBuilt(Node root)
        {
            // Nodes carry registered Godot scripts now, so a Duplicate keeps the
            // script instance and its lifecycle callbacks working.
            return root.Duplicate();
        }

        private ParsedScene LoadAndParse(string scenePath, string resolvedModId)
        {
            var cacheKey = scenePath;

            if (_parsedCache.TryGetValue(cacheKey, out var cached))
            {
                GameLogger.Log("TSCN", $"TscnSceneResolver.LoadAndParse: HIT parsed cache '{cacheKey}'", LogLevel.Debug);
                return cached;
            }

            var mod = Context.GetMod(resolvedModId);
            var provider = mod?.FileProvider;
            if (provider == null)
            {
                GameLogger.Log("TSCN", $"TscnSceneResolver.LoadAndParse: provider NOT FOUND for mod '{resolvedModId}'", LogLevel.Error);
                return null;
            }

            GameLogger.Log("TSCN", $"TscnSceneResolver.LoadAndParse: reading '{scenePath}' from mod='{resolvedModId}', provider={provider.GetType().Name}");

            var text = provider.ReadFileText(scenePath);
            if (text == null)
            {
                GameLogger.Log("TSCN", $"TscnSceneResolver.LoadAndParse: file text is NULL for path='{scenePath}'", LogLevel.Error);
                return null;
            }

            GameLogger.Log("TSCN", $"TscnSceneResolver.LoadAndParse: got {text.Length} chars, parsing...");
            var parsed = ApplyScenePatches(scenePath, _parser.Parse(text));
            _parsedCache[cacheKey] = parsed;
            GameLogger.Log("TSCN", $"TscnSceneResolver.LoadAndParse: parsed OK — {parsed.ExtResources.Count} ext_resources, {parsed.SubResources.Count} sub_resources, {parsed.Nodes.Count} nodes, entry_point='{parsed.EntryPointClass ?? "none"}'");

            return parsed;
        }

        /// <summary>
        /// Applies all scene patches targeting <paramref name="scenePath"/> to the freshly parsed
        /// scene, before it is cached. Patch layers cascade in mod load order (see
        /// <see cref="ScenePatchResolver"/>); a scene without patches passes through untouched.
        /// </summary>
        private ParsedScene ApplyScenePatches(string scenePath, ParsedScene parsed)
        {
            var patchResolver = Context?.GetResolver<ScenePatchResolver>();
            var patches = patchResolver?.GetPatches(scenePath);
            if (patches == null || patches.Count == 0)
                return parsed;

            var applier = new ScenePatchApplier();
            if (applier.ApplyAll(parsed, patches))
                GameLogger.Log("TSCN", $"TscnSceneResolver: applied {patches.Count} patch layer(s) to '{scenePath}'");
            else
                GameLogger.Log("TSCN", $"TscnSceneResolver: patch layers for '{scenePath}' reported errors", LogLevel.Warning);

            return parsed;
        }

        /// <summary>
        /// True when at least one mod patches this scene, including native <c>res://</c> scenes.
        /// The static facade uses it to route patched native scenes through the runtime builder
        /// instead of Godot's native loader.
        /// </summary>
        public bool HasScenePatches(string scenePath)
        {
            var patchResolver = Context?.GetResolver<ScenePatchResolver>();
            return patchResolver?.HasPatches(NormalizeResourcePath(scenePath)) == true;
        }

        /// <summary>
        /// Builds a native <c>res://</c> scene through the runtime pipeline so its patches apply.
        /// Native scenes normally bypass <see cref="TscnParser"/> via <see cref="LoadNative"/>,
        /// which is exactly why a patched one must be read from disk and patched before build.
        /// Parsed and built results are cached like mod scenes.
        /// </summary>
        private Node BuildNativePatched(string scenePath)
        {
            if (_builtCache.TryGetValue(scenePath, out var cached))
                return (Node)cached.Duplicate();

            if (!_buildingPaths.Add(scenePath))
            {
                GameLogger.Log("TSCN", $"TscnSceneResolver: circular reference detected for '{scenePath}'", LogLevel.Error);
                return null;
            }

            try
            {
                var text = ReadNativeText(scenePath);
                if (text == null)
                {
                    GameLogger.Log("TSCN", $"TscnSceneResolver.BuildNativePatched: cannot read 'res://{scenePath}'", LogLevel.Error);
                    return null;
                }

                var parsed = ApplyScenePatches(scenePath, _parser.Parse(text));
                _parsedCache[scenePath] = parsed;

                var previousScene = _resolvingScene;
                _resolvingScene = parsed;
                var root = new SceneBuilder().Build(parsed, InstanceLoader, ResolveExtResource);
                _resolvingScene = previousScene;

                if (root != null)
                {
                    _builtCache[scenePath] = root;
                    GameLogger.Log("TSCN", $"TscnSceneResolver.BuildNativePatched: OK '{scenePath}' → root='{root.Name}' ({root.GetType().Name})");
                    return (Node)root.Duplicate();
                }

                return null;
            }
            finally
            {
                _buildingPaths.Remove(scenePath);
            }
        }

        private static string ReadNativeText(string scenePath)
        {
            if (!GameEnvironment.IsRuntime)
                return null;

            try
            {
                return Godot.FileAccess.GetFileAsString("res://" + scenePath);
            }
            catch (Exception ex)
            {
                GameLogger.Log("TSCN", $"TscnSceneResolver: failed to read 'res://{scenePath}': {ex.Message}", LogLevel.Error);
                return null;
            }
        }

        private Node InstanceLoader(string scenePath)
        {
            return LoadAndBuild(scenePath);
        }

        /// <summary>
        /// Resolves ext_resource references for non-Script/non-PackedScene types (Texture2D, etc.).
        /// Looks up the ext_resource by refId in the currently building scene, normalises the path
        /// (strips res:// prefix), searches all mods in priority order for the file, reads raw bytes
        /// via IModFileProvider, and constructs the appropriate Godot Resource.
        /// </summary>
        private Resource ResolveExtResource(string refId)
        {
            if (_resolvingScene == null) return null;

            var extRes = _resolvingScene.ExtResources.FirstOrDefault(r => r.Id == refId);
            if (extRes == null) return null;

            var path = NormalizeResourcePath(extRes.Path);
            GameLogger.Log("TSCN", $"TscnSceneResolver.ResolveExtResource: refId='{refId}' type='{extRes.Type}' path='{extRes.Path}' → norm='{path}'", LogLevel.Debug);

            var provider = ResolveProviderForPath(path, out var modId);
            if (provider == null)
            {
                GameLogger.Log("TSCN", $"TscnSceneResolver.ResolveExtResource: provider NOT FOUND for '{path}'", LogLevel.Warning);
                return null;
            }

            var bytes = provider.ReadFileBinary(path);
            if (bytes == null)
            {
                GameLogger.Log("TSCN", $"TscnSceneResolver.ResolveExtResource: bytes NULL for '{path}'", LogLevel.Warning);
                return null;
            }

            var builder = ExtResourceBuilderRegistry.Get(extRes.Type)
                ?? ExtResourceBuilderRegistry.Get("Texture");
            if (builder == null)
            {
                GameLogger.Log("TSCN", $"TscnSceneResolver.ResolveExtResource: no builder for type='{extRes.Type}'", LogLevel.Warning);
                return null;
            }

            var resource = builder.Build(bytes, path);
            GameLogger.Log("TSCN", $"TscnSceneResolver.ResolveExtResource: OK type='{extRes.Type}' → {resource?.GetType().Name ?? "null"}", LogLevel.Debug);
            return resource;
        }

        /// <summary>
        /// Canonicalises a resource path: forward slashes, no <c>res://</c> scheme, no leading
        /// slash and no <c>mods/&lt;mod&gt;/</c> prefix. The last form is what the Godot editor
        /// writes when a file of one mod references a file of another mod, so stripping it lets
        /// editor-authored <c>ext_resource</c> paths and patch additions resolve unchanged.
        /// </summary>
        private static string NormalizeResourcePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            var n = path.Replace('\\', '/');
            if (n.StartsWith("res://", StringComparison.OrdinalIgnoreCase))
                n = n.Substring(6);
            n = n.TrimStart('/');

            var segments = n.Split('/');
            if (segments.Length >= 3 && segments[0].Equals("mods", StringComparison.OrdinalIgnoreCase))
                n = string.Join("/", segments, 2, segments.Length - 2);

            return n;
        }

        /// <summary>
        /// Fallback for scenes that still live in the project (res://) rather than
        /// in a mod. Godot's <c>ResourceLoader</c> cannot see files inside
        /// <c>mods/</c>, but native PackedScenes referenced from mod scenes (or
        /// deliberately left native) must still instantiate, mirroring
        /// PackedScene's instantiate-per-call contract for the resolver.
        /// </summary>
        private static Node LoadNative(string scenePath)
        {
            var resPath = "res://" + scenePath;
            var ext = Path.GetExtension(resPath);
            if (!ext.Equals(".tscn", StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(".scn", StringComparison.OrdinalIgnoreCase)
                && !ext.Equals(".escn", StringComparison.OrdinalIgnoreCase))
                return null;

            if (!ResourceLoader.Exists(resPath))
                return null;

            var packed = ResourceLoader.Load<PackedScene>(resPath);
            return packed?.Instantiate();
        }

        private IModFileProvider ResolveProviderForPath(string scenePath, out string modId)
        {
            var orderedIds = Context.GetOrderedModIds();

            foreach (var id in orderedIds)
            {
                var mod = Context.GetMod(id);
                if (mod?.FileProvider == null)
                    continue;

                if (mod.FileProvider.FileExists(scenePath))
                {
                    modId = id;
                    return mod.FileProvider;
                }
            }

            foreach (var kvp in Context.Mods)
            {
                if (kvp.Value.FileProvider?.FileExists(scenePath) == true)
                {
                    modId = kvp.Key;
                    return kvp.Value.FileProvider;
                }
            }

            modId = null;
            return null;
        }

        /// <summary>
        /// Drops the cached parse/build results for a single scene so the next
        /// <see cref="LoadAndBuild"/> call re-reads the source file from disk and
        /// rebuilds the node tree. Used by tooling after the scene text has been
        /// edited and saved, without flushing the whole resolver cache.
        /// </summary>
        /// <param name="scenePath">Mod-relative scene path (e.g. "scenes/foo.tscn").</param>
        public void Invalidate(string scenePath)
        {
            scenePath = NormalizeResourcePath(scenePath);
            _parsedCache.Remove(scenePath);
            _builtCache.Remove(scenePath);
        }

        /// <inheritdoc />
        public override void ClearCache()
        {
            _sceneIndex = null;
            _parsedCache.Clear();
            _builtCache.Clear();
            _buildingPaths.Clear();
            _resolvingScene = null;
        }
    }

    /// <summary>
    /// Static convenience accessor for <see cref="TscnSceneResolver"/>,
    /// proxying through <see cref="ModManager.Instance.GetResolver{T}"/>.
    /// </summary>
    public static class TscnScenes
    {
        /// <summary>
        /// Loads and builds a scene by mod-relative path. No mod id parameter:
        /// the resolver locates the owning mod by walking loaded mods in
        /// load-order priority (native <c>res://</c> scenes fall back to the
        /// engine loader, unless patched). Returns null when nothing resolves.
        /// </summary>
        public static Node LoadAndBuild(string scenePath)
            => ModManager.Instance.GetResolver<TscnSceneResolver>()?.LoadAndBuild(scenePath);

        /// <summary>
        /// True when the scene is patched, including native <c>res://</c> scenes that normally
        /// bypass the runtime pipeline. Scene routing uses this to send patched native scenes
        /// through <see cref="LoadAndBuild"/>.
        /// </summary>
        public static bool HasPatches(string scenePath)
            => ModManager.Instance.GetResolver<TscnSceneResolver>()?.HasScenePatches(scenePath) ?? false;
    }
}
