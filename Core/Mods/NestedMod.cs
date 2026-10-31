namespace Cthangover.Core.Mods
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Cthangover.Core.Mods.Providers;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Builds the virtual mod of a folder inside a host mod — a test suite (<see cref="ModTestSuites"/>)
    /// or an integration (<see cref="ModIntegrations"/>): the folder's own <c>manifest.json</c>, read
    /// through a provider rooted at the folder (a folder host gets a disk provider, a zip host a
    /// sub-directory view), with an implicit dependency on the host and the host's version and author
    /// when the manifest gives none. Neither kind nests: the result never declares suites or integrations.
    /// </summary>
    public static class NestedMod
    {
        /// <summary>
        /// The virtual mod <paramref name="id"/> from <paramref name="folder"/> of <paramref name="host"/>, or
        /// null (ERROR logged as <paramref name="kind"/>) when the folder has no readable manifest.
        /// <paramref name="defaultName"/> is used when the manifest has no name.
        /// </summary>
        public static ModInfo Create(ModInfo host, string id, string folder, string kind, string defaultName)
        {
            // Folder is a mod path ('/'-separated); a disk path gets the OS separators, or Windows ends up with "…\test_suites/walk".
            IModFileProvider provider = host.FileProvider?.RootPath != null && Directory.Exists(host.FileProvider.RootPath)
                ? new FolderModFileProvider(Path.Combine(host.FileProvider.RootPath, folder.Replace('/', Path.DirectorySeparatorChar)), id)
                : new SubdirModFileProvider(host.FileProvider, folder, id);

            if (!provider.FileExists("manifest.json"))
            {
                GameLogger.Log("MODS_REGISTRY", $"{kind} '{id}': '{host.Id}/{folder}/manifest.json' not found", LogLevel.Error);
                provider.Dispose();
                return null;
            }

            ModManifest manifest;
            try
            {
                manifest = JsonSerializer.Deserialize<ModManifest>(provider.ReadFileText("manifest.json"), JsonOptionsProvider.ReadOptions) ?? new ModManifest();
            }
            catch (JsonException ex)
            {
                GameLogger.Log("MODS_REGISTRY", $"{kind} '{id}': bad manifest.json: {ex.Message}", LogLevel.Error);
                provider.Dispose();
                return null;
            }

            manifest.Id = id;
            manifest.Version = string.IsNullOrEmpty(manifest.Version) ? host.Manifest?.Version : manifest.Version;
            manifest.Name = string.IsNullOrEmpty(manifest.Name) ? defaultName : manifest.Name;
            manifest.Author ??= host.Author;
            manifest.Depends ??= new List<ModDependency>();
            if (manifest.Depends.All(d => !string.Equals(d.ModId, host.Id, StringComparison.OrdinalIgnoreCase)))
                manifest.Depends.Insert(0, ModDependency.Parse(host.Id));
            manifest.TestSuites = null;
            manifest.Integrations = null;

            return new ModInfo
            {
                Id = id,
                Name = manifest.Name,
                Author = manifest.Author,
                Description = manifest.Description,
                FileProvider = provider,
                Manifest = manifest,
            };
        }
    }
}
