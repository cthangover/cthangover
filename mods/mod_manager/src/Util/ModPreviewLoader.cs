using System;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Caches;
using Godot;

namespace Mods.ModManager
{
    /// <summary>
    /// Loads preview images for mod tiles and the detail page. Local mods use
    /// their bundled <c>mod.png</c> / <c>mod.jpg</c>; catalog-only mods fall back
    /// to <see cref="ModIconCache"/>, which downloads and caches the entry's
    /// <c>icon_url</c> under <c>user://mod_cache/icons</c>. Returning null is a
    /// normal outcome — the card then keeps its flat placeholder.
    /// </summary>
    public static class ModPreviewLoader
    {
        /// <summary>Reads the mod's bundled preview image, or null when absent/unreadable.</summary>
        public static Texture2D LoadLocal(ModInfo info)
        {
            var provider = info?.FileProvider;
            if (provider == null)
                return null;

            byte[] data = null;
            if (provider.FileExists("mod.png"))
                data = provider.ReadFileBinary("mod.png");
            else if (provider.FileExists("mod.jpg"))
                data = provider.ReadFileBinary("mod.jpg");

            if (data == null || data.Length == 0)
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

        /// <summary>
        /// Asynchronously resolves a catalog icon; the callback may fire later
        /// (network) or immediately (memory/disk cache).
        /// </summary>
        public static void LoadRemote(string modId, string iconUrl, Action<Texture2D> onLoaded)
        {
            if (string.IsNullOrEmpty(iconUrl))
                return;

            ModIconCache.GetOrDownload(modId, iconUrl, onLoaded);
        }
    }
}
