namespace Cthangover.Core.Mods.Providers
{
    /// <summary>Managed ZIP mount with exact-case paths, independent streams and PKWare/WinZip AES password support.</summary>
    public class ZipModFileProvider : ArchiveModFileProvider
    {
        /// <summary>Indexes the package; encrypted entry contents are checked on reading.</summary>
        public ZipModFileProvider(string zipPath, string modName, string password = null)
            : base(zipPath, modName, password) { }
    }
}
