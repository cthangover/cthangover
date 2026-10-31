namespace Cthangover.Core.Mods
{
    using System.IO;

    /// <summary>A local package that discovery could not open; its filename remains visible even with encrypted headers.</summary>
    public sealed class UnavailableModArchive
    {
        /// <summary>Absolute local path; identity is independent of the encrypted manifest.</summary>
        public string Path { get; }
        /// <summary>Display name available before the manifest is readable.</summary>
        public string Name => System.IO.Path.GetFileName(Path);
        /// <summary>True for encryption errors; these can also indicate damaged encrypted content.</summary>
        public bool NeedsPassword { get; }

        /// <summary>Records a failed local package without storing exception text or secrets.</summary>
        public UnavailableModArchive(string path, bool needsPassword)
        {
            Path = System.IO.Path.GetFullPath(path);
            NeedsPassword = needsPassword;
        }
    }
}
