using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cthangover.Core.Factories;
using Cthangover.Core.Scenes;
using Cthangover.Core.Mods.Providers;
using Godot;
using Cthangover.Core.Mods.Caches;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    public class ModManager
    {
        private static readonly Lazy<ModManager> instance = new(() => new ModManager());
        public static ModManager Instance => instance.Value;

        private ModRegistry registry;

        private ModManager()
        {
            registry = ModRegistry.Instance;
        }

        public bool IsInitialized => ModRegistry.Instance.IsInitialized;

        public IReadOnlyDictionary<string, IModInfo> Mods => registry.Mods;

        public void Initialize()
        {
            registry.Initialize();
        }

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
        }

        public IModInfo GetMod(string id)
        {
            return registry.GetMod(id);
        }

        public bool FileExists(string modId, string path)
        {
            var mod = GetMod(modId);
            return mod?.FileProvider?.FileExists(path) ?? false;
        }

        public IEnumerable<string> ListFiles(string modId, string directory = "")
        {
            var mod = GetMod(modId);
            if (mod == null)
                return Enumerable.Empty<string>();

            return mod.FileProvider.ListFiles(directory);
        }

        public string ReadFileText(string modId, string path)
        {
            var mod = GetMod(modId);
            if (mod == null)
                return null;

            return mod.FileProvider.ReadFileText(path);
        }

        public byte[] ReadFileBinary(string modId, string path)
        {
            var mod = GetMod(modId);
            if (mod == null)
                return null;

            return mod.FileProvider.ReadFileBinary(path);
        }

        public Stream OpenStream(string modId, string path)
        {
            var mod = GetMod(modId);
            if (mod == null)
                return null;

            return mod.FileProvider.OpenStream(path);
        }

        public string GetFileSystemPath(string modId, string path)
        {
            var mod = GetMod(modId);
            if (mod == null)
                return null;

            return mod.FileProvider.GetFileSystemPath(path);
        }

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

        public Shader ResolveShader(string shaderName)
        {
            if (resolvedShaderCache.TryGetValue(shaderName, out var cached) && GodotObject.IsInstanceValid(cached))
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

        private Dictionary<string, FileEntry> textureFileCache;

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

        public Texture2D ResolveTexture(string textureName)
        {
            if (resolvedTextureCache.TryGetValue(textureName, out var cached) && GodotObject.IsInstanceValid(cached))
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

    }
}
