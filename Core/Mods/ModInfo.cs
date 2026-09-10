using System.Collections.Generic;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Runtime model for a loaded mod, combining the manifest metadata
    /// with the live <c>IModFileProvider</c> handle. <c>DisplayTitle</c>
    /// falls back to <c>Id</c> when <c>Name</c> is absent so that UI
    /// panels always have something human-readable to show even for
    /// minimally-authored mods.
    /// </summary>
    public class ModInfo : IModInfo
    {
        /// <summary>Unique mod identifier (folder name or zip name minus extension).</summary>
        public string Id { get; set; }

        /// <summary>Human-readable name from manifest.json.</summary>
        public string Name { get; set; }

        /// <summary>Author credit from manifest.json.</summary>
        public string Author { get; set; }

        /// <summary>Short description from manifest.json.</summary>
        public string Description { get; set; }

        /// <summary>The storage backend handle — folder or zip — that reads this mod's files.</summary>
        public IModFileProvider FileProvider { get; set; }

        /// <summary>Parsed manifest.json contents.</summary>
        public ModManifest Manifest { get; set; }

        /// <summary>Semver mod version from manifest, forwarded to
        /// <see cref="IModInfo.Version"/> for save-system version checks.</summary>
        public string Version => Manifest?.Version;

        /// <summary>
        /// Reasons why this mod failed to load. Collected during
        /// <c>FilterDisabledMods</c> and <c>ValidateDependencies</c>.
        /// Each reason is a separate entry — never overwritten, only appended.
        /// UI shows the full list.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public List<string> LoadFailureReasons { get; set; } = new();

        /// <summary>
        /// SHA256 checksum of the mod's installed content, computed during
        /// discovery by <see cref="ModHasher.ComputeInstallSha256(IModFileProvider, List{string})"/>.
        /// Used to detect local modifications by comparing against the
        /// <c>install_sha256</c> value in the online catalog. Null when
        /// the mod failed to load or the computation was skipped.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string InstallSha256 { get; set; }

        /// <summary>
        /// Safe UI label: attempts translation via <c>mod_{Id}_name</c> key first,
        /// falls back to <c>Name</c>, then to <c>Id</c>.
        /// </summary>
        public string DisplayTitle
        {
            get
            {
                if (!string.IsNullOrEmpty(Id) && GameEnvironment.IsRuntime)
                {
                    try
                    {
                        var key = $"mod_{Id}_name";
                        var translated = TranslationServer.Translate(key);
                        if (translated != key)
                            return translated;
                    }
                    catch
                    {
                        // Godot not initialized — fall through to default logic
                    }
                }
                return string.IsNullOrEmpty(Name) ? Id : Name;
            }
        }
    
        /// <summary>
        /// Short description in the current locale: attempts translation via
        /// <c>mod_{Id}_desc</c> key first, falls back to raw <c>Description</c>.
        /// </summary>
        public string LocalizedDescription
        {
            get
            {
                if (!string.IsNullOrEmpty(Id) && GameEnvironment.IsRuntime)
                {
                    try
                    {
                        var key = $"mod_{Id}_desc";
                        var translated = TranslationServer.Translate(key);
                        if (translated != key)
                            return translated;
                    }
                    catch
                    {
                        // Godot not initialized — fall through to default logic
                    }
                }
                return Description ?? string.Empty;
            }
        }
    }
}
