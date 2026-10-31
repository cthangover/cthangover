namespace Cthangover.Core.Mods.Providers
{
    /// <summary>Managed local 7z mount supporting AES data/header encryption and sequential solid-block reads.</summary>
    public sealed class SevenZipModFileProvider : ArchiveModFileProvider
    {
        /// <summary>Parses metadata; encrypted headers require the password before any file can be listed.</summary>
        public SevenZipModFileProvider(string archivePath, string modName, string password = null)
            : base(archivePath, modName, password) { }
    }
}
