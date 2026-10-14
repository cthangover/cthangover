using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods.Caches
{
    /// <summary>
    /// On-disk and in-memory cache for mod preview icons fetched from
    /// remote catalog entries. Each icon is keyed by <c>mod_id</c> and
    /// stored as a PNG file under <c>user://mod_cache/icons/</c>.
    ///
    /// Three-tier lookup for <see cref="GetOrDownloadAsync"/>:
    /// <list type="number">
    ///   <item>In-memory <see cref="Texture2D"/> dictionary (fast repeat access)</item>
    ///   <item>On-disk PNG file (survives game restart)</item>
    ///   <item>HTTP download from the catalog's <c>icon_url</c> field</item>
    /// </list>
    ///
    /// <see cref="Clear"/> drops both tiers and is called by
    /// <see cref="CatalogService"/> when the catalog is force-refreshed,
    /// so stale icons for removed mods don't linger on disk.
    /// </summary>
    public static class ModIconCache
    {
        private static readonly System.Net.Http.HttpClient _httpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(15),
            DefaultRequestHeaders = { { "User-Agent", "Cthangover/1.0" } },
        };

        private static readonly Dictionary<string, Texture2D> _memoryCache = new();

        private const string IconsSubDir = "icons";

        /// <summary>Globalised mod-cache root (<see cref="ModConfig.Cache"/>.<c>Root</c>), with an OS-profile fallback outside the runtime.</summary>
        public static string GetCacheRoot()
        {
            if (!GameEnvironment.IsRuntime)
                return Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                    "cthangover", "mod_cache");

            return ProjectSettings.GlobalizePath(ModConfig.Instance.Cache.Root);
        }

        /// <summary>Icons subdirectory under the cache root.</summary>
        public static string GetIconsDir()
        {
            return Path.Combine(GetCacheRoot(), IconsSubDir);
        }

        /// <summary>Disk path of the cached PNG preview for one mod id.</summary>
        public static string GetIconPath(string modId)
        {
            return Path.Combine(GetIconsDir(), $"{modId}.png");
        }

        /// <summary>
        /// Returns the cached <see cref="Texture2D"/> for the given mod,
        /// downloading it from <paramref name="iconUrl"/> if necessary.
        /// Returns <c>null</c> when <paramref name="iconUrl"/> is empty
        /// or the download fails.
        /// </summary>
        public static async Task<Texture2D> GetOrDownloadAsync(string modId, string iconUrl)
        {
            if (string.IsNullOrEmpty(modId) || string.IsNullOrEmpty(iconUrl))
                return null;

            if (_memoryCache.TryGetValue(modId, out var cached))
                return cached;

            var diskPath = GetIconPath(modId);
            byte[] imageData = null;

            if (File.Exists(diskPath))
            {
                imageData = File.ReadAllBytes(diskPath);
            }
            else
            {
                try
                {
                    imageData = await _httpClient.GetByteArrayAsync(iconUrl);
                    if (imageData != null && imageData.Length > 0)
                    {
                        var dir = GetIconsDir();
                        if (!Directory.Exists(dir))
                            Directory.CreateDirectory(dir);
                        File.WriteAllBytes(diskPath, imageData);
                        GameLogger.Log("ICON_CACHE", $"Downloaded icon for mod '{modId}'");
                    }
                }
                catch (Exception ex)
                {
                    GameLogger.Log("ICON_CACHE", $"Failed to download icon for mod '{modId}': {ex.Message}", LogLevel.Warning);
                    return null;
                }
            }

            if (imageData == null || imageData.Length == 0)
                return null;

            var texture = DecodeImage(imageData, modId);
            if (texture != null)
                _memoryCache[modId] = texture;

            return texture;
        }

        /// <summary>
        /// Synchronous version. In non-runtime (test) mode, downloads
        /// block the calling thread. In runtime, this is a fire-and-forget
        /// wrapper best used from callbacks that don't need the result.
        /// </summary>
        public static void GetOrDownload(string modId, string iconUrl, Action<Texture2D> onComplete)
        {
            if (string.IsNullOrEmpty(modId) || string.IsNullOrEmpty(iconUrl))
            {
                onComplete?.Invoke(null);
                return;
            }

            if (_memoryCache.TryGetValue(modId, out var cached))
            {
                onComplete?.Invoke(cached);
                return;
            }

            _ = DownloadAndCompleteAsync(modId, iconUrl, onComplete);
        }

        private static async Task DownloadAndCompleteAsync(string modId, string iconUrl, Action<Texture2D> onComplete)
        {
            var texture = await GetOrDownloadAsync(modId, iconUrl);
            onComplete?.Invoke(texture);
        }

        /// <summary>
        /// Drops all in-memory cached textures and deletes the on-disk
        /// icons directory. Called by <see cref="CatalogService"/> on
        /// force-refresh so that icons for removed or renamed mods are
        /// re-fetched from the updated catalog.
        /// </summary>
        public static void Clear()
        {
            _memoryCache.Clear();

            var iconsDir = GetIconsDir();
            if (Directory.Exists(iconsDir))
            {
                try
                {
                    Directory.Delete(iconsDir, recursive: true);
                    GameLogger.Log("ICON_CACHE", "Cleared icon cache");
                }
                catch (Exception ex)
                {
                    GameLogger.Log("ICON_CACHE", $"Failed to clear icon cache: {ex.Message}", LogLevel.Warning);
                }
            }
        }

        private static Texture2D DecodeImage(byte[] data, string modId)
        {
            if (!GameEnvironment.IsRuntime)
                return null;

            var image = new Image();
            Error error;

            if (data[0] == 0x89)
                error = image.LoadPngFromBuffer(data);
            else if (data[0] == 0xFF)
                error = image.LoadJpgFromBuffer(data);
            else
                return null;

            if (error != Error.Ok)
            {
                image.Dispose();
                return null;
            }

            var texture = ImageTexture.CreateFromImage(image);
            image.Dispose();
            return texture;
        }
    }
}
