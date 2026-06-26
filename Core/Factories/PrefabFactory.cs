using System;
using System.Collections.Generic;
using System.IO;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

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
    /// </summary>
    public abstract class PrefabFactory<T> : IPrefabFactory<T>, ICacheLoader<string, T> where T : class
    {
        private readonly BoundedCache<string, T> _cache;

        protected PrefabFactory(string factoryKey, int fallbackCacheSize)
        {
            var size = ModConfig.Instance.Cache.GetCacheSize(factoryKey, fallbackCacheSize);
            _cache = new BoundedCache<string, T>(size, this);
        }

        public abstract string GroupName { get; }

        protected abstract T ConvertFromBytes(string id, byte[] data, string extension);

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

        public virtual T Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;

            var result = _cache.Get(id);
            if (result == null && !id.EndsWith("_albedo", StringComparison.OrdinalIgnoreCase) && !id.EndsWith("_depth", StringComparison.OrdinalIgnoreCase))
                GameLogger.Log("FACTORY", $"resource from factory '{GetType().Name}' with id '{id}' - can't get in group '{GroupName}'!", LogLevel.Error);
            
            return result;
        }

        protected virtual T LoadFromMods(string id, string extension)
        {
            var files = ModManager.Instance.CollectFileList(GroupName);

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
