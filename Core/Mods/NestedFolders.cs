using System;
using System.Collections.Generic;
using System.Linq;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// The folders of a host mod that belong to its nested mods: test suites (<see cref="ModTestSuites"/>) and
    /// integrations (<see cref="ModIntegrations"/>). Each is mounted as its own virtual mod with its own files, so
    /// a scan of the host must not read them: an entity or a source file inside a suite folder joins the host only
    /// when the suite is mounted. The default parents <c>test_suites/</c> and <c>integrations/</c> always belong to
    /// nested mods, declared or not (a folder that is not declared is not mounted, and must not leak into the host
    /// either); a custom <c>path</c> from the manifest is added on top of them.
    /// </summary>
    public static class NestedFolders
    {
        /// <summary>
        /// The nested folders of <paramref name="manifest"/>: normalised mod-relative paths without a trailing
        /// <c>/</c>, compared case-insensitively. Always contains the default <c>test_suites</c> and
        /// <c>integrations</c> parents, and the declared folders of the manifest (null manifest: the defaults only).
        /// </summary>
        public static IReadOnlyCollection<string> Of(ModManifest manifest)
        {
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Add(folders, ModTestSuites.FolderName);
            Add(folders, ModIntegrations.FolderName);
            foreach (var suite in manifest?.TestSuites ?? new List<ModTestSuite>())
            {
                if (suite != null && !string.IsNullOrWhiteSpace(suite.Id))
                    Add(folders, suite.Folder);
            }

            foreach (var integration in manifest?.Integrations ?? new List<ModIntegration>())
            {
                if (integration != null && !string.IsNullOrWhiteSpace(integration.Id))
                    Add(folders, integration.Folder);
            }

            return folders;
        }

        /// <summary>
        /// The files of <paramref name="mod"/> as a scan of the host sees them: its provider with the nested folders
        /// left out of listings, and null for a null mod.
        /// </summary>
        public static IModFileProvider Visible(IModInfo mod)
            => Visible(mod?.FileProvider, (mod as ModInfo)?.Manifest);

        /// <summary>
        /// <paramref name="provider"/> with the nested folders of <paramref name="manifest"/> hidden from listings
        /// (see <see cref="Of"/>). Reads of explicit paths are not affected; the nested mod reads its own files
        /// through its own provider, never through this view.
        /// </summary>
        public static IModFileProvider Visible(IModFileProvider provider, ModManifest manifest)
        {
            if (provider == null)
                return null;

            return new HidingProvider(provider, Of(manifest));
        }

        /// <summary>True when the mod-relative <paramref name="path"/> is one of the folders or lies inside one.</summary>
        public static bool IsNested(string path, IReadOnlyCollection<string> folders)
        {
            if (folders == null || folders.Count == 0 || string.IsNullOrEmpty(path))
                return false;

            var normalized = ModPath.Normalize(path.TrimEnd('/'));
            return folders.Any(folder => string.Equals(normalized, folder, StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith(folder + "/", StringComparison.OrdinalIgnoreCase));
        }

        private static void Add(HashSet<string> folders, string folder)
        {
            var normalized = ModPath.Normalize((folder ?? string.Empty).TrimEnd('/'));
            if (normalized.Length > 0)
                folders.Add(normalized);
        }

        /// <summary>
        /// A read-only view of a mod's provider: listings skip the nested folders (a listing of a nested folder itself
        /// is empty). Everything else delegates. It does not own the inner provider, so <see cref="Dispose"/> does nothing.
        /// </summary>
        private sealed class HidingProvider : IModFileProvider
        {
            private readonly IModFileProvider _inner;
            private readonly IReadOnlyCollection<string> _folders;

            public HidingProvider(IModFileProvider inner, IReadOnlyCollection<string> folders)
            {
                _inner = inner;
                _folders = folders;
            }

            public string Mod => _inner.Mod;

            public string RootPath => _inner.RootPath;

            public bool FileExists(string path) => _inner.FileExists(path);

            public IEnumerable<string> ListFiles(string directory = "")
            {
                if (IsNested(directory, _folders))
                    return Enumerable.Empty<string>();

                return _inner.ListFiles(directory).Where(entry => !IsNested(entry, _folders));
            }

            public string ReadFileText(string path) => _inner.ReadFileText(path);

            public byte[] ReadFileBinary(string path) => _inner.ReadFileBinary(path);

            public System.IO.Stream OpenStream(string path) => _inner.OpenStream(path);

            public string GetFileSystemPath(string path) => _inner.GetFileSystemPath(path);

            public string ResolveIncludePath(string includePath, string currentPath) => _inner.ResolveIncludePath(includePath, currentPath);

            public void Dispose()
            {
            }
        }
    }
}
