using System;
using System.IO;
using Cthangover.Core.Mods.Providers;

namespace Cthangover.Core.Mods.Discovery
{
    /// <summary>
    /// Loads mods stored as <c>.zip</c> archives. <see cref="IsCanProvide"/>
    /// matches any path ending with <c>.zip</c> (case-insensitive).
    /// Creates a <see cref="ZipModFileProvider"/> which builds an O(1)
    /// lookup of all zip entries on construction.
    ///
    /// Zip mods cannot serve Godot scenes directly —
    /// <c>GetFileSystemPath</c> returns <c>null</c>. Callers that need
    /// resource loading (factories, scene instantiation) must either
    /// extract assets to the cache or fall back to folder mods.
    /// </summary>
    public class ZipModLoader : IModLoader
    {
        /// <inheritdoc />
        public string Name => "Zip";

        /// <summary>
        /// Returns <c>true</c> for any path that ends with <c>.zip</c>
        /// (case-insensitive comparison).
        /// </summary>
        public bool IsCanProvide(string url)
        {
            return url.EndsWith(".zip", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Creates a <see cref="ZipModFileProvider"/> that opens the zip
        /// archive and builds an in-memory entry lookup. The archive
        /// remains open until <c>Dispose</c> is called.
        /// </summary>
        public IModFileProvider CreateProvider(string url, string modName)
        {
            return new ZipModFileProvider(url, modName);
        }

        /// <summary>
        /// Strips the <c>.zip</c> extension from the entry name to
        /// produce the fallback mod ID — the filename is typically the
        /// mod's canonical identifier.
        /// </summary>
        public string GetFallbackModId(string url, string entryName)
        {
            return Path.GetFileNameWithoutExtension(entryName);
        }
    }
}
