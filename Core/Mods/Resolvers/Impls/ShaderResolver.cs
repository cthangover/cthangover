using System;
using System.Collections.Generic;
using Cthangover.Core.Mods.Caches;
using Cthangover.Core.Mods.Providers;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods.Resolvers
{
    /// <summary>
    /// Collects and resolves <c>.gdshader</c> files from mods. Uses
    /// <see cref="FileListResolver"/> to get the raw list of shader files
    /// grouped by logical name, then resolves each one to a Godot
    /// <see cref="Shader"/> resource.
    ///
    /// For folder mods the shader is loaded directly via
    /// <c>GD.Load</c> from the filesystem path. For zip mods the shader
    /// is extracted to the mod cache first via
    /// <see cref="ModCacheManager.ExtractShaders"/>. Caching is two-layer:
    /// a resolved-cache for already-loaded <see cref="Shader"/> instances,
    /// and the disk-based mod cache for extracted files.
    /// </summary>
    public class ShaderResolver : ModAssetResolver
    {
        /// <inheritdoc />
        public override string Id => "shaders";

        private Dictionary<string, FileEntry> _fileCache;
        private readonly Dictionary<string, Shader> _resolvedCache = new();

        /// <summary>
        /// Collects all shader file entries from mods via
        /// <see cref="FileListResolver"/>. Filters to <c>.gdshader</c>
        /// files only (excluding <c>.gdshaderinclude</c>). Caches the
        /// result so subsequent calls return the same dictionary.
        /// </summary>
        public Dictionary<string, FileEntry> Collect()
        {
            Context.Initialize();

            if (_fileCache != null)
                return _fileCache;

            var fileListResolver = Context.GetResolver<FileListResolver>();
            var raw = fileListResolver?.Collect("shaders") ?? new Dictionary<string, FileEntry>();
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

            _fileCache = result;
            GameLogger.Log("MODS", $"CollectShaders → {result.Count} shader(s)");
            return result;
        }

        /// <summary>
        /// Resolves a shader by logical name. Returns the Godot
        /// <see cref="Shader"/> resource, loading from disk cache or
        /// extracting from the mod provider as needed.
        /// </summary>
        public Shader Resolve(string shaderName)
        {
            var cached = TryGetCached(shaderName);
            if (cached != null)
                return cached;

            if (ModCacheManager.IsShaderCached(shaderName))
            {
                var cachedPath = ModCacheManager.GetShaderCachePath(shaderName);
                var cachedShader = GD.Load<Shader>(cachedPath);
                if (cachedShader != null)
                {
                    _resolvedCache[shaderName] = cachedShader;
                    return cachedShader;
                }
            }

            var shaders = Collect();
            if (!shaders.TryGetValue(shaderName, out var entry))
            {
                GameLogger.Log("MODS", $"ResolveShader: '{shaderName}' not found in any mod", LogLevel.Error);
                return null;
            }

            var mod = Context.GetMod(entry.ModId);
            if (mod == null)
            {
                GameLogger.Log("MODS", $"ResolveShader: owning mod '{entry.ModId}' of '{shaderName}' is not loaded", LogLevel.Error);
                return null;
            }

            if (mod.FileProvider is FolderModFileProvider)
            {
                var fsPath = mod.FileProvider.GetFileSystemPath(entry.FullPath);
                if (fsPath == null)
                {
                    GameLogger.Log("MODS", $"ResolveShader: '{shaderName}' file '{entry.FullPath}' missing on disk in mod '{entry.ModId}'", LogLevel.Error);
                    return null;
                }

                var shader = GD.Load<Shader>(fsPath);
                if (shader != null)
                    _resolvedCache[shaderName] = shader;
                return shader;
            }

            ModCacheManager.ExtractShaders(entry.ModId, mod.FileProvider);

            if (ModCacheManager.IsShaderCached(shaderName))
            {
                var extractedPath = ModCacheManager.GetShaderCachePath(shaderName);
                var extractedShader = GD.Load<Shader>(extractedPath);
                if (extractedShader != null)
                    _resolvedCache[shaderName] = extractedShader;
                return extractedShader;
            }

            GameLogger.Log("MODS", $"ResolveShader: '{shaderName}' failed to extract from mod '{entry.ModId}'", LogLevel.Error);

            return null;
        }

        /// <summary>
        /// Returns a previously resolved shader from the in-memory cache,
        /// validated with <c>GodotObject.IsInstanceValid</c>. Returns
        /// <c>null</c> if not cached or the cached instance was freed.
        /// </summary>
        public Shader TryGetCached(string shaderName)
        {
            if (_resolvedCache.TryGetValue(shaderName, out var cached) && GodotObject.IsInstanceValid(cached))
                return cached;
            return null;
        }

        /// <inheritdoc />
        public override void ClearCache()
        {
            _fileCache = null;
            _resolvedCache.Clear();
        }
    }

    /// <summary>
    /// Static convenience accessor for <see cref="ShaderResolver"/>,
    /// proxying through <see cref="ModManager.Instance.GetResolver{T}"/>.
    /// </summary>
    public static class Shaders
    {
        public static Dictionary<string, FileEntry> Collect()
            => ModManager.Instance.GetResolver<ShaderResolver>()?.Collect();

        public static Shader Resolve(string name)
            => ModManager.Instance.GetResolver<ShaderResolver>()?.Resolve(name);

        public static Shader TryGetCached(string name)
            => ModManager.Instance.GetResolver<ShaderResolver>()?.TryGetCached(name);
    }
}
