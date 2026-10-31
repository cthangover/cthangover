using System;
using System.IO;
using Cthangover.Core.Mods.Providers;

namespace Cthangover.Core.Mods.Discovery
{
    /// <summary>
    /// Discovers local 7z packages at boot, including archives whose entry names
    /// require a password before the manifest can be found. No native 7-Zip installation is needed.
    /// </summary>
    public class SevenZipModLoader : IModLoader
    {
        private readonly ArchivePasswordStore passwords;

        /// <summary>Discovery uses the user credential store warmed by boot.</summary>
        public SevenZipModLoader() { }

        /// <summary>Uses a caller-owned credential store, allowing isolated hosts and tests.</summary>
        public SevenZipModLoader(ArchivePasswordStore passwords) => this.passwords = passwords;

        /// <inheritdoc />
        public string Name => "7z";

        /// <inheritdoc />
        public bool IsCanProvide(string url) =>
            url != null && url.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) && !Directory.Exists(url);

        /// <inheritdoc />
        public IModFileProvider CreateProvider(string url, string modName) =>
            new SevenZipModFileProvider(url, modName, (passwords ?? ArchivePasswordStore.Default).Get(url));

        /// <inheritdoc />
        public string GetFallbackModId(string url, string entryName) => Path.GetFileNameWithoutExtension(entryName);
    }
}
