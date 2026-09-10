using System;
using System.Collections.Generic;
using System.IO;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods.Resolvers
{
    /// <summary>
    /// Collects and resolves texture files (PNG, JPG, WebP) from mods.
    /// Textures are grouped into logical directories defined by
    /// <see cref="ModConfig.TextureGroups"/> (e.g. "textures/portraits",
    /// "textures/backgrounds"). Files are collected via
    /// <see cref="FileListResolver"/>, filtered by the configured
    /// texture extensions, and deduplicated — later mods in load order
    /// override earlier ones with the same logical name.
    ///
    /// Resolution decodes the raw image bytes into a Godot
    /// <see cref="ImageTexture"/> and caches the result in memory.
    /// </summary>
    public class TextureResolver : ModAssetResolver
    {
        /// <inheritdoc />
        public override string Id => "textures";

        private Dictionary<string, FileEntry> _fileCache;
        private readonly Dictionary<string, Texture2D> _resolvedCache = new();

        /// <summary>
        /// Collects all texture file entries from every configured
        /// texture group directory. Deduplicates by logical name
        /// (filename without extension) — later mods in load order win.
        /// Caches the result for subsequent calls.
        /// </summary>
        public Dictionary<string, FileEntry> Collect()
        {
            Context.Initialize();

            if (_fileCache != null)
                return _fileCache;

            var result = new Dictionary<string, FileEntry>();
            var textureGroups = ModConfig.Instance.TextureGroups;
            var textureExts = ModConfig.Instance.GetTextureExtensionSet();

            var fileListResolver = Context.GetResolver<FileListResolver>();
            if (fileListResolver == null)
            {
                _fileCache = result;
                return result;
            }

            foreach (var group in textureGroups)
            {
                var files = fileListResolver.Collect(group);
                foreach (var kvp in files)
                {
                    var ext = Path.GetExtension(kvp.Key);
                    if (textureExts.Contains(ext))
                    {
                        var textureName = Path.GetFileNameWithoutExtension(kvp.Key);
                        if (result.ContainsKey(textureName))
                        {
                            GameLogger.Log("MODS", $"texture conflict: '{textureName}' from mod '{kvp.Value.ModId}' overrides previous", LogLevel.Warning);
                            ModConflictRegistry.Record("texture", textureName, kvp.Value.ModId, result[textureName].ModId, false);
                        }
                        result[textureName] = kvp.Value;
                    }
                }
            }

            _fileCache = result;
            GameLogger.Log("MODS", $"CollectTextures → {result.Count} texture(s)");

            return result;
        }

        /// <summary>
        /// Resolves a texture by logical name, decoding the raw image
        /// bytes into a Godot <see cref="ImageTexture"/>. Supports PNG,
        /// JPG, and WebP formats. Returns <c>null</c> on decode failure
        /// or if the texture is not found in any mod.
        ///
        /// <see cref="Collect"/> keys textures by basename only, so an
        /// ID with a directory prefix (e.g. <c>"prolog/cloak_ground"</c>)
        /// would miss on the exact lookup. As a fallback the ID is also
        /// matched against the full relative path without extension and
        /// against the basename, letting callers address textures the same
        /// way the editor's <c>BackgroundFactory</c> does.
        /// </summary>
        public Texture2D Resolve(string textureName)
        {
            var cached = TryGetCached(textureName);
            if (cached != null)
                return cached;

            var textures = Collect();
            if (!textures.TryGetValue(textureName, out var entry))
            {
                entry = FindByPathOrBasename(textures, textureName);
                if (entry.ModId == null)
                {
                    GameLogger.Log("MODS", $"ResolveTexture: '{textureName}' not found in any mod", LogLevel.Error);
                    return null;
                }
            }

            var data = Context.ReadFileBinary(entry.ModId, entry.FullPath);
            if (data == null)
            {
                GameLogger.Log("MODS", $"ResolveTexture: '{textureName}' could not be read from mod '{entry.ModId}'", LogLevel.Error);
                return null;
            }

            var ext = Path.GetExtension(entry.FullPath)?.ToLowerInvariant();
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
                _resolvedCache[textureName] = texture;

            return texture;
        }

        /// <summary>
        /// Locates a texture entry when the exact basename lookup misses.
        /// First tries the full relative path without extension (so
        /// <c>"prolog/cloak_ground"</c> matches <c>"prolog/cloak_ground.png"</c>),
        /// then falls back to the bare filename (so a flat <c>"lamp"</c> ID keeps
        /// working regardless of which texture group directory it lives in).
        /// Matching is case-insensitive. Returns a default <see cref="FileEntry"/>
        /// when nothing matches.
        /// </summary>
        private static FileEntry FindByPathOrBasename(Dictionary<string, FileEntry> textures, string textureName)
        {
            var normalized = textureName.Replace('\\', '/');

            foreach (var kvp in textures)
            {
                var pathNoExt = Path.ChangeExtension(kvp.Key, null)?.TrimEnd('.');
                if (string.Equals(pathNoExt, normalized, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            foreach (var kvp in textures)
            {
                var baseName = Path.GetFileNameWithoutExtension(kvp.Key);
                if (string.Equals(baseName, textureName, StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            return default;
        }

        /// <summary>
        /// Returns a previously resolved texture from the in-memory cache,
        /// validated with <c>GodotObject.IsInstanceValid</c>.
        /// </summary>
        public Texture2D TryGetCached(string textureName)
        {
            if (_resolvedCache.TryGetValue(textureName, out var cached) && GodotObject.IsInstanceValid(cached))
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
    /// Static convenience accessor for <see cref="TextureResolver"/>,
    /// proxying through <see cref="ModManager.Instance.GetResolver{T}"/>.
    /// </summary>
    public static class Textures
    {
        public static Dictionary<string, FileEntry> Collect()
            => ModManager.Instance.GetResolver<TextureResolver>()?.Collect();

        public static Texture2D Resolve(string name)
            => ModManager.Instance.GetResolver<TextureResolver>()?.Resolve(name);

        public static Texture2D TryGetCached(string name)
            => ModManager.Instance.GetResolver<TextureResolver>()?.TryGetCached(name);
    }
}
