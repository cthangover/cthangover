using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Mods.Providers;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Mounts the integration test suites of discovered mods
    /// (<see cref="ModManifest.TestSuites"/>). A suite is a folder inside its
    /// host (<c>test_suites/&lt;id&gt;/</c>) with an ordinary <c>manifest.json</c>
    /// (sources, gd_sources, ui, depends, …) and content laid out like a mod.
    /// When active it becomes the virtual mod <c>&lt;host&gt;__&lt;id&gt;</c> that
    /// implicitly depends on its host, so compilation, load order, resource
    /// loaders and the atlases treat it like any mod — one that sits above its
    /// host and may depend on mods the host itself must not (a test-only
    /// <c>depends</c>, without cycles).
    /// <para/>
    /// Gate: a suite is mounted only when a user arg starts with one of its
    /// <c>activate</c> prefixes, or with <see cref="AllFlag"/> (atlases).
    /// Without that the suite folder is inert data of the host: a normal
    /// launch, a game and <c>--all-mods</c> never load it.
    /// </summary>
    public static class ModTestSuites
    {
        /// <summary>Default parent folder of suites inside the host mod.</summary>
        public const string FolderName = "test_suites";

        /// <summary>Mounts every suite (used by the resource and locale atlases).</summary>
        public const string AllFlag = "--with-test-suites";

        private const string Separator = "__";

        private static readonly List<string> activeIds = new();

        /// <summary>Virtual mod ids mounted by the last <see cref="Mount"/>; added to a game/front-end set like <c>--with-mods</c>.</summary>
        public static IReadOnlyList<string> ActiveIds => activeIds;

        /// <summary>Virtual mod id of a suite: <c>&lt;host&gt;__&lt;suite&gt;</c>.</summary>
        public static string ModIdOf(string hostId, string suiteId) => hostId + Separator + suiteId;

        /// <summary>True when <paramref name="userArgs"/> mount the suite. Pure.</summary>
        public static bool IsActive(ModTestSuite suite, IEnumerable<string> userArgs)
        {
            if (suite == null || string.IsNullOrWhiteSpace(suite.Id))
                return false;
            var args = userArgs as ICollection<string> ?? (userArgs ?? Array.Empty<string>()).ToList();
            if (args.Contains(AllFlag))
                return true;
            return (suite.Activate ?? new List<string>())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Any(prefix => args.Any(a => a.StartsWith(prefix, StringComparison.Ordinal)));
        }

        /// <summary>
        /// Adds the active suites of <paramref name="mods"/> as virtual mods.
        /// Runs once per boot right after discovery, before the set filters.
        /// </summary>
        public static void Mount(IDictionary<string, IModInfo> mods, IReadOnlyCollection<string> userArgs)
        {
            activeIds.Clear();
            foreach (var host in mods.Values.OfType<ModInfo>().ToList())
            {
                foreach (var suite in host.Manifest?.TestSuites ?? new List<ModTestSuite>())
                {
                    if (!IsActive(suite, userArgs))
                        continue;

                    var info = CreateSuiteMod(host, suite);
                    if (info == null)
                        continue;
                    if (mods.ContainsKey(info.Id))
                    {
                        GameLogger.Log("MODS_REGISTRY", $"Test suite '{info.Id}' clashes with an installed mod id; skipped", LogLevel.Error);
                        info.FileProvider.Dispose();
                        continue;
                    }

                    mods[info.Id] = info;
                    activeIds.Add(info.Id);
                    GameLogger.Log("MODS_REGISTRY", $"Test suite '{info.Id}' mounted from '{host.Id}/{suite.Folder}'");
                }
            }
        }

        /// <summary>
        /// Builds the virtual mod of one suite, or null when its folder or
        /// manifest is missing. Pure apart from reading the host's files.
        /// </summary>
        public static ModInfo CreateSuiteMod(ModInfo host, ModTestSuite suite)
        {
            var id = ModIdOf(host.Id, suite.Id);
            var folder = suite.Folder;
            // Folder is a mod path ('/'-separated); a disk path gets the OS separators, or Windows ends up with "…\test_suites/walk".
            IModFileProvider provider = host.FileProvider?.RootPath != null && Directory.Exists(host.FileProvider.RootPath)
                ? new FolderModFileProvider(Path.Combine(host.FileProvider.RootPath, folder.Replace('/', Path.DirectorySeparatorChar)), id)
                : new SubdirModFileProvider(host.FileProvider, folder, id);

            if (!provider.FileExists("manifest.json"))
            {
                GameLogger.Log("MODS_REGISTRY", $"Test suite '{id}': '{host.Id}/{folder}/manifest.json' not found", LogLevel.Error);
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
                GameLogger.Log("MODS_REGISTRY", $"Test suite '{id}': bad manifest.json: {ex.Message}", LogLevel.Error);
                provider.Dispose();
                return null;
            }

            manifest.Id = id;
            manifest.Version = string.IsNullOrEmpty(manifest.Version) ? host.Manifest?.Version : manifest.Version;
            manifest.Name = string.IsNullOrEmpty(manifest.Name) ? $"{host.Name ?? host.Id} — test {suite.Id}" : manifest.Name;
            manifest.Author ??= host.Author;
            manifest.Depends ??= new List<ModDependency>();
            if (manifest.Depends.All(d => !string.Equals(d.ModId, host.Id, StringComparison.OrdinalIgnoreCase)))
                manifest.Depends.Insert(0, ModDependency.Parse(host.Id));
            manifest.TestSuites = null; // suites do not nest

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
