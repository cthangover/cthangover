using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cthangover.Core.Factories;
using Cthangover.Core.Interactive;
using Cthangover.Core.Scenes;
using Cthangover.Core.Mods.Providers;
using Godot;
using Cthangover.Core.Mods.Caches;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Central hub for all mod resource access. Every factory, every
    /// scene loader, every asset resolver ultimately goes through
    /// <c>ModManager</c> to find and read files. It wraps
    /// <c>ModRegistry</c> and adds a layer of domain-specific
    /// collection methods with their own caching.
    ///
    /// <b>File collection layer</b> — <c>CollectFileList</c> scans all
    /// mods for files under a named group directory (e.g. "avatars",
    /// "music"). Results are cached per group so factories don't
    /// re-scan on every request. Later-loaded mods' files override
    /// earlier ones — the last mod to define a file wins.
    ///
    /// <b>JSON data layer</b> — <c>CollectJsonGroup</c> reads every
    /// <c>.json</c> file in a directory across all mods, deserialises
    /// the <c>FileData&lt;T&gt;</c> envelope, and builds an ID-indexed
    /// dictionary. This is the backbone of every <c>FileFactory&lt;T&gt;</c>
    /// and many <c>ICacheLoader</c>-based factories.
    ///
    /// <b>Asset resolution layer</b> — <c>CollectTextures</c>,
    /// <c>CollectShaders</c>, <c>ResolveTexture</c>, <c>ResolveShader</c>
    /// go beyond raw file listing: they merge files from multiple
    /// texture groups, strip extensions, handle Godot resource loading
    /// from folder mods vs zip extraction for shaders, and maintain
    /// resolved-resource caches that survive across cache clears.
    ///
    /// <b>Include resolution</b> — <c>ExpandModIncludes</c> processes
    /// <c>${path}</c> macros recursively with circular-reference
    /// detection, enabling mod JSON files to compose and share data
    /// fragments without duplicating content.
    ///
    /// <b>Scene/scenario collection</b> — <c>CollectSceneDefinitions</c>
    /// and <c>CollectScenarioDefinitions</c> recursively walk the
    /// "scenes" and "scenarios" directories, parsing JSON and
    /// <c>.scenario</c> metadata respectively, building the data
    /// structures that drive the scene loading and dialog systems.
    ///
    /// On <c>Reload</c>, every cache is flushed so that hot-reloaded
    /// mods are picked up without restarting the game. The
    /// <c>resolvedTextureCache</c> and <c>resolvedShaderCache</c>
    /// entries are validated with <c>GodotObject.IsInstanceValid</c>
    /// because Godot resources can be freed by the engine and the C#
    /// wrapper becomes a dangling pointer.
    /// </summary>
    public class ModManager
    {
        private static readonly Lazy<ModManager> instance = new(() => new ModManager());
        /// <summary>Thread-safe singleton.</summary>
        public static ModManager Instance => instance.Value;

        private ModRegistry registry;

        private ModManager()
        {
            registry = ModRegistry.Instance;
        }

        /// <summary>True if <c>ModRegistry</c> has completed its discovery scan.</summary>
        public bool IsInitialized => ModRegistry.Instance.IsInitialized;

        /// <summary>Read-only snapshot of currently loaded mods, keyed by mod ID.</summary>
        public IReadOnlyDictionary<string, IModInfo> Mods => registry.Mods;

        /// <summary>Idempotent: triggers mod discovery if not yet done.</summary>
        public void Initialize()
        {
            registry.Initialize();
        }

        /// <summary>
        /// Flushes every internal cache then re-runs mod discovery.
        /// Call after adding or removing mod files at runtime.
        /// </summary>
        public void Reload()
        {
            ClearCaches();
            registry.Reload();
        }

        private void ClearCaches()
        {
            fileListCache.Clear();
            jsonGroupCache.Clear();
            sceneDefCache.Clear();
            scenarioDefCache.Clear();
            shaderFileCache = null;
            resolvedShaderCache.Clear();
            textureFileCache = null;
            resolvedTextureCache.Clear();
            wrapperCache.Clear();
            interactiveDefCache.Clear();
        }

        /// <summary>Looks up a mod by ID. Implicitly triggers discovery if needed.</summary>
        public IModInfo GetMod(string id)
        {
            return registry.GetMod(id);
        }

        /// <summary>Checks whether a file exists within a specific mod.</summary>
        public bool FileExists(string modId, string path)
        {
            var mod = GetMod(modId);
            return mod?.FileProvider?.FileExists(path) ?? false;
        }

        /// <summary>
        /// Lists files and directories immediately under the given path
        /// within a mod. Directories have a trailing <c>/</c>.
        /// </summary>
        public IEnumerable<string> ListFiles(string modId, string directory = "")
        {
            var mod = GetMod(modId);
            if (mod == null)
                return Enumerable.Empty<string>();

            return mod.FileProvider.ListFiles(directory);
        }

        /// <summary>Reads a text file from a mod as UTF-8, or null if not found.</summary>
        public string ReadFileText(string modId, string path)
        {
            var mod = GetMod(modId);
            if (mod == null)
                return null;

            return mod.FileProvider.ReadFileText(path);
        }

        /// <summary>Reads a binary file from a mod as a byte array, or null if not found.</summary>
        public byte[] ReadFileBinary(string modId, string path)
        {
            var mod = GetMod(modId);
            if (mod == null)
                return null;

            return mod.FileProvider.ReadFileBinary(path);
        }

        /// <summary>Opens a seekable stream to a file inside a mod.</summary>
        public Stream OpenStream(string modId, string path)
        {
            var mod = GetMod(modId);
            if (mod == null)
                return null;

            return mod.FileProvider.OpenStream(path);
        }

        /// <summary>
        /// Returns an absolute filesystem path for a mod file, or null
        /// for zip mods (where no real path exists).
        /// </summary>
        public string GetFileSystemPath(string modId, string path)
        {
            var mod = GetMod(modId);
            if (mod == null)
                return null;

            return mod.FileProvider.GetFileSystemPath(path);
        }

        /// <summary>
        /// Reads a text file and expands <c>${include}</c> macros
        /// recursively with circular-reference detection. Returns the
        /// fully-expanded text, or null on any failure.
        /// </summary>
        public string ReadResolvedText(string modId, string path)
        {
            var mod = GetMod(modId);
            if (mod == null)
                return null;

            var content = mod.FileProvider.ReadFileText(path);
            if (content == null)
                return null;

            try
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path };
                    return ExpandModIncludes(mod.FileProvider, content, path, visited);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS", $"Include resolution failed for '{modId}/{path}': {ex.Message}", LogLevel.Error);
                return null;
            }
        }

        /// <summary>
        /// Same as <c>ReadResolvedText(string, string)</c> but uses an
        /// externally-provided provider instead of looking up the mod.
        /// </summary>
        public string ReadResolvedText(string modId, string path, IModFileProvider providerOverride)
        {
            var content = providerOverride.ReadFileText(path);
            if (content == null)
                return null;

            try
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path };
                    return ExpandModIncludes(providerOverride, content, path, visited);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS", $"Include resolution failed for '{modId}/{path}': {ex.Message}", LogLevel.Error);
                return null;
            }
        }

        /// <summary>
        /// Reads a JSON file from a mod, resolves includes, and
        /// deserialises it into <typeparamref name="T"/>.
        /// </summary>
        public T ReadJson<T>(string modId, string path) where T : class
        {
            var text = ReadResolvedText(modId, path);
            if (text == null)
                return null;

            try
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return JsonSerializer.Deserialize<T>(text, options);
            }
            catch (JsonException ex)
            {
                GameLogger.Log("MODS", $"JSON deserialize failed for '{modId}/{path}': {ex.Message}", LogLevel.Error);
                return null;
            }
        }

        private readonly Dictionary<string, Dictionary<string, FileEntry>> fileListCache = new();

        /// <summary>
        /// Scans all mods for files under a named group directory
        /// (e.g. "avatars", "music"). Results are cached; later
        /// mods override earlier ones. Used by <c>PrefabFactory</c>
        /// as the first stage of asset lookup.
        /// </summary>
        public Dictionary<string, FileEntry> CollectFileList(string group)
        {
            Initialize();

            if (fileListCache.TryGetValue(group, out var cached))
                return cached;

            GameLogger.Log("MODS", $"CollectFileList('{group}'): CACHE MISS, collecting from {Mods.Count} mod(s)...");

            var result = new Dictionary<string, FileEntry>();
            var prefix = group.Replace('\\', '/').TrimEnd('/') + "/";

            foreach (var kvp in Mods)
            {
                var modId = kvp.Key;
                var provider = kvp.Value.FileProvider;
                var before = result.Count;

                CollectGroupFiles(provider, modId, group, prefix, result);

                if (result.Count > before)
                {
                    GameLogger.Log("MODS", $"mod '{modId}' contributed {result.Count - before} file(s) to group '{group}'");
                }
            }

            fileListCache[group] = result;

            GameLogger.Log("MODS", $"CollectFileList('{group}') → {result.Count} file(s), keys=[{string.Join(", ", result.Keys)}]");
            return result;
        }

        private Dictionary<string, FileEntry> shaderFileCache;

        /// <summary>
        /// Scans the "shaders" group across all mods, filters to
        /// <c>.gdshader</c> files only (excluding includes), and
        /// strips the extension to produce short shader names.
        /// </summary>
        public Dictionary<string, FileEntry> CollectShaders()
        {
            Initialize();

            if (shaderFileCache != null)
                return shaderFileCache;

            var raw = CollectFileList("shaders");
            var result = new Dictionary<string, FileEntry>();

            foreach (var kvp in raw)
            {
                if (kvp.Key.EndsWith(".gdshader", StringComparison.OrdinalIgnoreCase)
                    && !kvp.Key.EndsWith(".gdshaderinclude", StringComparison.OrdinalIgnoreCase))
                {
                    var shaderName = kvp.Key.Substring(0, kvp.Key.Length - ".gdshader".Length);
                    result[shaderName] = kvp.Value;
                }
            }

            shaderFileCache = result;
            GameLogger.Log("MODS", $"CollectShaders → {result.Count} shader(s)");
            return result;
        }

        private readonly Dictionary<string, Shader> resolvedShaderCache = new();

		/// <summary>
		/// Resolves a shader name to a Godot <c>Shader</c> resource.
		/// First checks the resolved cache (validating with
		/// <c>IsInstanceValid</c>), then the on-disk cache, then
		/// loads directly for folder mods or extracts from zip for
		/// zip mods.
		/// </summary>
		public Shader ResolveShader(string shaderName)
		{
			var cached = TryGetCachedShader(shaderName);
			if (cached != null)
				return cached;

			if (ModCacheManager.IsShaderCached(shaderName))
			{
				var cachedPath = ModCacheManager.GetShaderCachePath(shaderName);
				var cachedShader = GD.Load<Shader>(cachedPath);
				if (cachedShader != null)
				{
					resolvedShaderCache[shaderName] = cachedShader;
					return cachedShader;
				}
			}

			var shaders = CollectShaders();
			if (!shaders.TryGetValue(shaderName, out var entry))
				return null;

			var mod = GetMod(entry.ModId);
			if (mod == null)
				return null;

			if (mod.FileProvider is FolderModFileProvider)
			{
				var fsPath = mod.FileProvider.GetFileSystemPath(entry.FullPath);
				if (fsPath == null)
					return null;

				var shader = GD.Load<Shader>(fsPath);
				if (shader != null)
					resolvedShaderCache[shaderName] = shader;
				return shader;
			}

			ModCacheManager.ExtractShaders(entry.ModId, mod.FileProvider);

			if (ModCacheManager.IsShaderCached(shaderName))
			{
				var extractedPath = ModCacheManager.GetShaderCachePath(shaderName);
				var extractedShader = GD.Load<Shader>(extractedPath);
				if (extractedShader != null)
					resolvedShaderCache[shaderName] = extractedShader;
				return extractedShader;
			}

			GameLogger.Log("MODS", $"ResolveShader: '{shaderName}' failed to extract from mod '{entry.ModId}'", LogLevel.Error);

			return null;
		}

		public Shader TryGetCachedShader(string shaderName)
		{
			if (resolvedShaderCache.TryGetValue(shaderName, out var cached) && GodotObject.IsInstanceValid(cached))
				return cached;
			return null;
		}

        private Dictionary<string, FileEntry> textureFileCache;

        /// <summary>
        /// Scans every texture group in <c>ModConfig.TextureGroups</c>
        /// across all mods, merges files into a flat namespace by
        /// stripping extensions, and logs conflicts where two mods
        /// define the same texture name.
        /// </summary>
        public Dictionary<string, FileEntry> CollectTextures()
        {
            Initialize();

            if (textureFileCache != null)
                return textureFileCache;

            var result = new Dictionary<string, FileEntry>();
            var textureGroups = ModConfig.Instance.TextureGroups;
            var textureExts = ModConfig.Instance.GetTextureExtensionSet();

            foreach (var group in textureGroups)
            {
                var files = CollectFileList(group);
                foreach (var kvp in files)
                {
                    var ext = System.IO.Path.GetExtension(kvp.Key);
                    if (textureExts.Contains(ext))
                    {
                        var textureName = System.IO.Path.GetFileNameWithoutExtension(kvp.Key);
                        if (result.ContainsKey(textureName))
                        {
                            GameLogger.Log("MODS", $"texture conflict: '{textureName}' from mod '{kvp.Value.ModId}' overrides previous", LogLevel.Warning);
                        }
                        result[textureName] = kvp.Value;
                    }
                }
            }

            textureFileCache = result;
            GameLogger.Log("MODS", $"CollectTextures → {result.Count} texture(s)");

            return result;
        }

        private readonly Dictionary<string, Texture2D> resolvedTextureCache = new();

        /// <summary>
        /// Resolves a texture name to a Godot <c>Texture2D</c>.
        /// Decodes the image from raw bytes, wraps it in an
        /// <c>ImageTexture</c>, and caches the result.
        /// </summary>
        public Texture2D ResolveTexture(string textureName)
        {
            var cached = TryGetCachedTexture(textureName);
            if (cached != null)
                return cached;

            var textures = CollectTextures();
            if (!textures.TryGetValue(textureName, out var entry))
                return null;

            var data = ReadFileBinary(entry.ModId, entry.FullPath);
            if (data == null)
            {
                GameLogger.Log("MODS", $"ResolveTexture: '{textureName}' could not be read from mod '{entry.ModId}'", LogLevel.Error);
                return null;
            }

            var ext = System.IO.Path.GetExtension(entry.FullPath)?.ToLowerInvariant();
            var image = new Image();
            Error loadError;
            if (ext == ".png")
                loadError = image.LoadPngFromBuffer(data);
            else if (ext == ".jpg" || ext == ".jpeg")
                loadError = image.LoadJpgFromBuffer(data);
            else if (ext == ".webp")
                loadError = image.LoadWebpFromBuffer(data);
            else
            {
                GameLogger.Log("MODS", $"ResolveTexture: unsupported extension '{ext}' for '{textureName}'", LogLevel.Error);
                return null;
            }

            if (loadError != Error.Ok)
            {
                GameLogger.Log("MODS", $"ResolveTexture: image decode failed for '{textureName}' ({loadError})", LogLevel.Error);
                return null;
            }

            var texture = ImageTexture.CreateFromImage(image);
            image.Dispose();
            if (texture != null)
                resolvedTextureCache[textureName] = texture;

            return texture;
        }

        public Texture2D TryGetCachedTexture(string textureName)
        {
            if (resolvedTextureCache.TryGetValue(textureName, out var cached) && GodotObject.IsInstanceValid(cached))
                return cached;
            return null;
        }

        private static void CollectGroupFiles(IModFileProvider provider, string modId, string dir, string prefix, Dictionary<string, FileEntry> result)
        {
            var entries = provider.ListFiles(dir).ToList();
            GameLogger.Log("MODS", $"[{modId}]  CollectGroupFiles: dir='{dir}', provider entries count={entries.Count}, prefix='{prefix}'", LogLevel.Debug);
            foreach (var entry in entries)
            {
                if (entry.EndsWith("/"))
                {
                    var subDir = entry.TrimEnd('/');

                    GameLogger.Log("MODS", $"    -> subdir '{subDir}'", LogLevel.Debug);

                    CollectGroupFiles(provider, modId, subDir, prefix, result);
                }
                else if (entry.StartsWith(prefix))
                {
                    var key = entry.Substring(prefix.Length);
                    if (result.ContainsKey(key))
                    {
                        GameLogger.Log("MODS", $"conflict: '{key}' in mod '{modId}' overrides previous entry", LogLevel.Warning);
                    }
                    result[key] = new FileEntry(modId, entry);
                    GameLogger.Log("MODS", $"    + file '{key}' <- {entry} (mod={modId})", LogLevel.Debug);
                }
                else
                {
                    GameLogger.Log("MODS", $"    SKIP '{entry}': does not start with prefix '{prefix}'", LogLevel.Warning);
                }
            }
        }

        private readonly Dictionary<string, object> jsonGroupCache = new();

        /// <summary>
        /// Scans a named directory across all mods for <c>.json</c>
        /// files, deserialises each as <c>FileData&lt;T&gt;</c>, and
        /// returns a flat ID-indexed dictionary. Later mods override
        /// earlier ones for duplicate IDs. The backbone of every
        /// <c>FileFactory&lt;T&gt;</c>.
        /// </summary>
        public Dictionary<string, T> CollectJsonGroup<T>(string directory) where T : class, IIdentifiable
        {
            Initialize();

            var cacheKey = typeof(T).FullName + "@" + directory;
            if (jsonGroupCache.TryGetValue(cacheKey, out var cached))
                return (Dictionary<string, T>)cached;

            GameLogger.Log("MODS", $"CollectJsonGroup<{typeof(T).Name}>('{directory}'): CACHE MISS, scanning {Mods.Count} mod(s)...");

            var result = new Dictionary<string, T>();
            var prefix = directory.Replace('\\', '/').TrimEnd('/') + "/";
            var foundAny = false;

            foreach (var kvp in Mods)
            {
                var modId = kvp.Key;
                var provider = kvp.Value.FileProvider;

                foreach (var entry in provider.ListFiles(directory))
                {
                    if (entry.EndsWith("/"))
                        continue;

                    var relativePath = entry.StartsWith(prefix) ? entry.Substring(prefix.Length) : entry;
                    if (!relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        continue;

                    GameLogger.Log("MODS", $"  Reading json: mod='{modId}', entry='{entry}'", LogLevel.Debug);
                    var jsonText = ReadResolvedText(modId, entry, provider);
                    if (jsonText == null)
                    {
                        GameLogger.Log("MODS", $"  ReadResolvedText returned null for '{modId}/{entry}'", LogLevel.Error);
                        continue;
                    }

                    try
                    {
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var fileData = JsonSerializer.Deserialize<FileData<T>>(jsonText, options);
                        if (fileData?.Items == null || fileData.Items.Count == 0)
                        {
                            GameLogger.Log("MODS", $"  No items in file '{entry}'", LogLevel.Error);
                            continue;
                        }

                        foundAny = true;
                        foreach (var item in fileData.Items)
                        {
                            if (result.ContainsKey(item.ID))
                            {
                                GameLogger.Log("MODS", $"'{item.ID}' from mod '{modId}' overrides previous definition", LogLevel.Warning);
                            }
                            result[item.ID] = item;

                            GameLogger.Log("MODS", $"  + item: id={item.ID}", LogLevel.Debug);
                        }
                    }
                    catch (JsonException ex)
                    {
                        GameLogger.Log("MODS", $"JSON parse failed for '{modId}/{entry}': {ex.Message}", LogLevel.Error);
                    }
                }
            }

            if (!foundAny)
            {
                GameLogger.Log("MODS", $"CollectJsonGroup<{typeof(T).Name}>('{directory}') — no data found", LogLevel.Error);
            }

            jsonGroupCache[cacheKey] = result;

            GameLogger.Log("MODS", $"CollectJsonGroup<{typeof(T).Name}>('{directory}') → {result.Count} item(s)");
            return result;
        }

        private static readonly Regex includePattern = new Regex(@"\$\{([^}]+)\}", RegexOptions.Compiled);

        private static string ExpandModIncludes(IModFileProvider provider, string content, string currentPath, HashSet<string> visited)
        {
            return includePattern.Replace(content, match =>
            {
                var includePath = match.Groups[1].Value.Trim();
                if (string.IsNullOrEmpty(includePath))
                    throw new InvalidOperationException("Empty include path in ${}.");

                var resolvedPath = provider.ResolveIncludePath(includePath, currentPath);
                var loaded = provider.ReadFileText(resolvedPath);
                if (loaded == null)
                    loaded = provider.ReadFileText(resolvedPath + ".json");
                if (loaded == null)
                    throw new InvalidOperationException($"Include not found: '{resolvedPath}' (from '${{{includePath}}}').");

                if (visited.Contains(resolvedPath))
                    throw new InvalidOperationException($"Circular include detected: {resolvedPath}");

                visited.Add(resolvedPath);
                try
                {
                    var expanded = ExpandModIncludes(provider, loaded, resolvedPath, visited);
                    return expanded.Trim();
                }
                finally
                {
                    visited.Remove(resolvedPath);
                }
            });
        }

        private readonly Dictionary<string, Dictionary<string, List<SceneDefinition>>> sceneDefCache = new();

        /// <summary>
        /// Recursively walks the "scenes" directory across all mods
        /// and deserialises every <c>.json</c> file as a
        /// <c>SceneDefinition</c>. Returns a dictionary keyed by
        /// scene name — multiple mods can contribute definitions
        /// for the same scene (they accumulate in a list).
        /// </summary>
        public Dictionary<string, List<SceneDefinition>> CollectSceneDefinitions()
        {
            Initialize();

            const string cacheKey = "__scenes__";
            if (sceneDefCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var result = new Dictionary<string, List<SceneDefinition>>();
            var foundAny = false;

            foreach (var kvp in Mods)
            {
                var modId = kvp.Key;
                var provider = kvp.Value.FileProvider;

                CollectSceneDefsRecursive(provider, modId, "scenes", result, ref foundAny);
            }

            if (!foundAny)
            {
                GameLogger.Log("MODS", "CollectSceneDefinitions — no scene definitions found", LogLevel.Error);
            }

            sceneDefCache[cacheKey] = result;
            GameLogger.Log("MODS", $"CollectSceneDefinitions → {result.Count} scene(s)");
            return result;
        }

        private void CollectSceneDefsRecursive(IModFileProvider provider, string modId, string dir, Dictionary<string, List<SceneDefinition>> result, ref bool foundAny)
        {
            foreach (var entry in provider.ListFiles(dir))
            {
                if (entry.EndsWith("/"))
                {
                    var subDir = entry.TrimEnd('/');
                    CollectSceneDefsRecursive(provider, modId, subDir, result, ref foundAny);
                    continue;
                }

                if (!entry.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                var text = ReadResolvedText(modId, entry, provider);
                if (text == null)
                    continue;

                try
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var def = System.Text.Json.JsonSerializer.Deserialize<SceneDefinition>(text, options);
                    if (def == null || string.IsNullOrEmpty(def.Name))
                        continue;

                    def.ModId = modId;
                    foundAny = true;

                    if (!result.TryGetValue(def.Name, out var list))
                    {
                        list = new List<SceneDefinition>();
                        result[def.Name] = list;
                    }
                    list.Add(def);

                    GameLogger.Log("MODS", $"scene '{def.Name}' from mod '{modId}' ({entry})");
                }
                catch (System.Text.Json.JsonException ex)
                {
                    GameLogger.Log("MODS", $"JSON parse failed for scene '{modId}/{entry}': {ex.Message}", LogLevel.Error);
                }
            }
        }

        private readonly Dictionary<string, Dictionary<string, List<ScenarioDefinition>>> scenarioDefCache = new();
        private readonly Dictionary<string, List<WrapperDefinition>> wrapperCache = new();
        private readonly Dictionary<string, Dictionary<string, InteractiveDefinition>> interactiveDefCache = new();

        /// <summary>
        /// Recursively walks the "scenarios" directory across all
        /// mods, parses <c>.scenario</c> file metadata headers, and
        /// returns scenario definitions grouped by target scene name.
        /// </summary>
        public Dictionary<string, List<ScenarioDefinition>> CollectScenarioDefinitions()
        {
            Initialize();

            const string cacheKey = "__scenarios__";
            if (scenarioDefCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var result = new Dictionary<string, List<ScenarioDefinition>>();

            foreach (var kvp in Mods)
            {
                var modId = kvp.Key;
                var provider = kvp.Value.FileProvider;

                CollectScenarioDefsRecursive(provider, modId, "scenarios", result);
            }

            scenarioDefCache[cacheKey] = result;

            GameLogger.Log("MODS", $"CollectScenarioDefinitions → {result.Count} scene(s) with scenarios");
            return result;
        }

        private void CollectScenarioDefsRecursive(IModFileProvider provider, string modId, string dir, Dictionary<string, List<ScenarioDefinition>> result)
        {
            foreach (var entry in provider.ListFiles(dir))
            {
                if (entry.EndsWith("/"))
                {
                    var subDir = entry.TrimEnd('/');
                    CollectScenarioDefsRecursive(provider, modId, subDir, result);
                    continue;
                }

                if (!entry.EndsWith(".scenario", StringComparison.OrdinalIgnoreCase))
                    continue;

                var text = provider.ReadFileText(entry);
                if (text == null)
                    continue;

                var (metadata, _) = Scenarios.ScenarioParser.ParseMetadata(text);
                if (!metadata.TryGetValue("scene", out var sceneName) || string.IsNullOrEmpty(sceneName))
                    continue;

                var name = Path.GetFileNameWithoutExtension(entry.Replace('\\', '/').Split('/')[^1]);

                var def = new ScenarioDefinition
                {
                    Name = name,
                    Scene = sceneName,
                    ModId = modId,
                    FilePath = entry,
                    Priority = metadata.TryGetValue("priority", out var p) && int.TryParse(p, out var prio) ? prio : 0,
                    After = metadata.TryGetValue("after", out var after) ? after : null,
                    Condition = metadata.TryGetValue("condition", out var cond) ? cond : null,
                    LightUseTime = metadata.TryGetValue("light_use_time", out var l) && bool.TryParse(l, out var b) ? b : null,
                    SaveAllowed = metadata.TryGetValue("save_allowed", out var sa) && bool.TryParse(sa, out var sab),
                    IsOneRun = metadata.TryGetValue("is_one_run", out var ior) && bool.TryParse(ior, out var iorb),
                };

                if (!result.TryGetValue(sceneName, out var list))
                {
                    list = new List<ScenarioDefinition>();
                    result[sceneName] = list;
                }
                list.Add(def);

                GameLogger.Log("MODS", $"scenario '{name}' -> scene '{sceneName}' from mod '{modId}' ({entry})");
            }
        }

        /// <summary>
        /// Recursively collects <c>.wrappertmpl</c> files from the
        /// "wrappers" directory across all mods. Each template is
        /// stored as raw text — parsing happens at instantiation time.
        /// </summary>
        public List<WrapperDefinition> CollectWrapperTemplates()
        {
            Initialize();

            const string cacheKey = "__wrappers__";
            if (wrapperCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var result = new List<WrapperDefinition>();

            foreach (var kvp in Mods)
            {
                var modId = kvp.Key;
                var provider = kvp.Value.FileProvider;
                CollectWrapperDefsRecursive(provider, modId, "wrappers", result);
            }

            wrapperCache[cacheKey] = result;
            return result;
        }

        private static void CollectWrapperDefsRecursive(IModFileProvider provider, string modId, string dir, List<WrapperDefinition> result)
        {
            foreach (var entry in provider.ListFiles(dir))
            {
                if (entry.EndsWith("/"))
                {
                    var subDir = entry.TrimEnd('/');
                    CollectWrapperDefsRecursive(provider, modId, subDir, result);
                    continue;
                }

                if (!entry.EndsWith(".wrappertmpl", StringComparison.OrdinalIgnoreCase))
                    continue;

                var text = provider.ReadFileText(entry);
                if (text == null)
                    continue;

                var name = System.IO.Path.GetFileNameWithoutExtension(entry.Replace('\\', '/').Split('/')[^1]);

                result.Add(new WrapperDefinition
                {
                    ModId = modId,
                    Name = name,
                    Content = text
                });
            }
        }

        /// <summary>
        /// Recursively walks the "interactives" directory across all mods,
        /// deserialises every <c>.json</c> file via the <c>FileData&lt;InteractiveDefinition&gt;</c>
        /// envelope, and returns a flat dictionary keyed by definition ID.
        /// Later mods override earlier ones for duplicate IDs.
        /// Each definition's <c>ModId</c> property is set to its owning mod.
        /// </summary>
        public Dictionary<string, InteractiveDefinition> CollectInteractives()
        {
            Initialize();

            const string cacheKey = "__interactives__";
            if (interactiveDefCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var result = new Dictionary<string, InteractiveDefinition>();

            foreach (var kvp in Mods)
            {
                var modId = kvp.Key;
                var provider = kvp.Value.FileProvider;

                CollectInteractiveDefsRecursive(provider, modId, "interactives", result);
            }

            interactiveDefCache[cacheKey] = result;
            GameLogger.Log("MODS", $"CollectInteractives -> {result.Count} definition(s)");
            return result;
        }

        private void CollectInteractiveDefsRecursive(IModFileProvider provider, string modId, string dir, Dictionary<string, InteractiveDefinition> result)
        {
            foreach (var entry in provider.ListFiles(dir))
            {
                if (entry.EndsWith("/"))
                {
                    var subDir = entry.TrimEnd('/');
                    CollectInteractiveDefsRecursive(provider, modId, subDir, result);
                    continue;
                }

                if (!entry.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                    continue;

                var text = ReadResolvedText(modId, entry, provider);
                if (text == null)
                    continue;

                try
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var fileData = JsonSerializer.Deserialize<FileData<InteractiveDefinition>>(text, options);
                    if (fileData?.Items == null || fileData.Items.Count == 0)
                        continue;

                    foreach (var item in fileData.Items)
                    {
                        if (string.IsNullOrEmpty(item.ID))
                            continue;

                        item.ModId = modId;

                        if (result.ContainsKey(item.ID))
                        {
                            GameLogger.Log("MODS", $"interactive '{item.ID}' from mod '{modId}' overrides previous definition", LogLevel.Warning);
                        }

                        result[item.ID] = item;
                        GameLogger.Log("MODS", $"interactive '{item.ID}' from mod '{modId}' ({entry})");
                    }
                }
                catch (JsonException ex)
                {
                    GameLogger.Log("MODS", $"JSON parse failed for interactive '{modId}/{entry}': {ex.Message}", LogLevel.Error);
                }
            }
        }

    }
}
