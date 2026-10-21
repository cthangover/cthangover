using System;
using System.Collections.Generic;
using System.IO;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Factories
{
    /// <summary>
    /// Core binary-asset factory with LRU-bounded caching and multi-extension
    /// mod file scanning. Implements <c>ICacheLoader</c> so that cache misses
    /// trigger a fresh scan of all mods — each registered file extension is
    /// tried in order and the first matching asset wins. The <c>factoryKey</c>
    /// passed to the constructor is used to look up a cache size override from
    /// <c>ModConfig</c>, allowing per-asset-type memory budgeting without
    /// hard-coding sizes per subclass. Subclasses implement
    /// <c>ConvertFromBytes</c> to transform raw file data into Godot resources.
    ///
    /// Falls back silently for <c>_albedo</c> / <c>_depth</c> suffix lookups
    /// (normal-map companion textures that may legitimately not exist for every
    /// sprite), avoiding log noise for optional secondary textures.
    ///
    /// Cached payloads are Godot resources (<c>Texture2D</c>, <c>AudioStream</c>,
    /// <c>PackedScene</c>) whose native/GPU memory outlives a plain .NET
    /// reference, so the LRU cache is wired to a disposal hook
    /// (<see cref="ReleaseResource"/>): when an entry leaves the cache and no
    /// engine object (sprite, audio player, ...) holds a native reference any
    /// more, the resource is disposed immediately instead of waiting for the
    /// .NET GC finalizer. Without the hook, VRAM and audio memory of evicted
    /// assets stay allocated until the next collection — which manifests as a
    /// leak during long sessions with frequent location changes.
    /// </summary>
    public abstract class PrefabFactory<T> : IPrefabFactory<T>, ICacheLoader<string, T> where T : class
    {
        private readonly BoundedCache<string, T> _cache;

        protected PrefabFactory(string factoryKey, int fallbackCacheSize)
        {
            var size = ModConfig.Instance.Cache.GetCacheSize(factoryKey, fallbackCacheSize);
            _cache = new BoundedCache<string, T>(size, this, ReleaseResource);
        }

        /// <summary>Mod asset group this factory scans (e.g. <c>"music"</c>, <c>"backgrounds"</c>); ids are relative to it.</summary>
        public abstract string GroupName { get; }

        /// <summary>Converts raw mod file bytes into the Godot resource this factory produces.</summary>
        protected abstract T ConvertFromBytes(string id, byte[] data, string extension);

        /// <summary>File extensions tried in order during the mod scan; the first matching asset wins.</summary>
        protected abstract List<string> Extensions { get; }

        T ICacheLoader<string, T>.Load(string id)
        {
            foreach (var extension in Extensions)
            {
                var result = LoadFromMods(id, extension);
                if (result != null)
                    return result;
            }
            
            if(id == null || (!id.EndsWith("_albedo", StringComparison.OrdinalIgnoreCase) && !id.EndsWith("_depth", StringComparison.OrdinalIgnoreCase)))
                GameLogger.Log("FACTORY", $"resource from factory '{GetType().Name}' with id '{id}' - not found in group '{GroupName}'!", LogLevel.Error);
            
            return null;
        }

        /// <summary>
        /// Returns the cached asset for <paramref name="id"/>, loading it through
        /// the LRU-bounded cache on first access. Missing assets are logged —
        /// except the optional <c>_albedo</c>/<c>_depth</c> companion lookups,
        /// which may legitimately not exist.
        /// </summary>
        public virtual T Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            var result = _cache.Get(id);
            if (result == null && !id.EndsWith("_albedo", StringComparison.OrdinalIgnoreCase) && !id.EndsWith("_depth", StringComparison.OrdinalIgnoreCase))
                GameLogger.Log("FACTORY", $"resource from factory '{GetType().Name}' with id '{id}' - can't get in group '{GroupName}'!", LogLevel.Error);
            
            return result;
        }

        /// <summary>
        /// Drops the cached asset <paramref name="id"/>, so the next <see cref="Get"/>
        /// reads the file again — for an asset whose file was replaced while the
        /// game runs. A value still shown by an engine object stays alive there
        /// (see <see cref="ReleaseResource"/>); only new lookups get the new file.
        /// </summary>
        public void Invalidate(string id)
        {
            if (!string.IsNullOrEmpty(id))
                _cache.Invalidate(id);
        }

        /// <summary>
        /// Drops every cached asset of this factory (same rules as
        /// <see cref="Invalidate"/>). Used after files of the group were added
        /// or replaced in bulk, together with clearing the file list
        /// (<see cref="FileListResolver.ClearCache"/>) that maps ids to files.
        /// </summary>
        public void Clear()
        {
            _cache.Clear();
        }

        /// <summary>
        /// BoundedCache eviction hook: releases the native/GPU memory of an
        /// evicted payload. Godot resources are ref-counted natively and the
        /// cache's managed wrapper owns exactly one of those references, so
        /// the resource is disposed only when that reference is the last one
        /// (<c>GetReferenceCount() == 1</c>) — a texture still assigned to a
        /// sprite or a stream still played by an audio player keeps its native
        /// owner and is left intact, its wrapper is finalised by the GC later.
        /// Direct external disposal of a cached value (mod code calling
        /// Dispose itself) is tolerated via <see cref="GodotObject.IsInstanceValid"/>.
        /// Consequence for callers: values must be handed to engine objects
        /// (node properties, players) rather than stored as bare C# fields for
        /// the whole session, otherwise an LRU eviction may dispose a wrapper
        /// that is still referenced from managed code only.
        /// </summary>
        private void ReleaseResource(T resource)
        {
            if (resource is RefCounted refCounted &&
                GodotObject.IsInstanceValid(refCounted) &&
                refCounted.GetReferenceCount() == 1)
            {
                refCounted.Dispose();
            }
        }

        /// <summary>Scans the group's collected files for <paramref name="id"/> plus the given extension and reads the matching mod file's bytes.</summary>
        protected virtual T LoadFromMods(string id, string extension)
        {
            var files = Files.Collect(GroupName);

            string matchedKey = null;
            string ext = null;

            if (extension != null)
            {
                matchedKey = id + extension;
            }
            else
            {
                foreach (var key in files.Keys)
                {
                    if (key == id || key.StartsWith(id + ".", StringComparison.OrdinalIgnoreCase))
                    {
                        matchedKey = key;
                        break;
                    }
                }
            }

            if (matchedKey == null || !files.TryGetValue(matchedKey, out var entry))
            {
                return default(T);
            }

            ext = Path.GetExtension(matchedKey);
            var bytes = ModManager.Instance.ReadFileBinary(entry.ModId, entry.FullPath);
            if (bytes == null)
            {
                GameLogger.Log("FACTORY", $"resource '{GetType().Name}' with id '{id}' - failed to read binary!", LogLevel.Error);
                return default(T);
            }

            return ConvertFromBytes(id, bytes, ext);
        }
    }
}
