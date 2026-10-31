using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods.Resolvers
{
    using System.Text;
    /// <summary>
    /// Provides resolvers with a unified API for accessing the mod
    /// registry, reading files from mod providers, resolving JSON with
    /// <c>${include}</c> directives, and looking up other resolvers.
    /// Created internally by <see cref="ModManager"/> and injected into
    /// each <see cref="IModAssetResolver"/> via
    /// <see cref="IModAssetResolver.Initialize"/>.
    ///
    /// All file I/O methods route through the mod's
    /// <see cref="IModFileProvider"/>, which means they work identically
    /// for folder mods and zip mods — the resolver never branches on
    /// storage format.
    /// <para/>
    /// The single-file reads also accept the id of a mounted
    /// <see cref="ModOverlays">overlay</see>: <see cref="FileListResolver"/>
    /// lists overlay files under the overlay id, so a factory reading a
    /// collected <see cref="FileEntry"/> (a background dropped into the
    /// scenario studio's edit layer) reaches the overlay's provider the same
    /// way it reaches a mod's.
    /// </summary>
    public class ModResolutionContext
    {
        private readonly ModRegistry _registry;
        private Dictionary<string, IModAssetResolver> _resolvers;

        internal ModResolutionContext(ModRegistry registry)
        {
            _registry = registry;
        }

        internal void SetResolvers(Dictionary<string, IModAssetResolver> resolvers)
        {
            _resolvers = resolvers;
        }

        /// <summary>Read-only snapshot of currently loaded mods.</summary>
        public IReadOnlyDictionary<string, IModInfo> Mods => _registry.Mods;

        /// <summary>Whether the registry has completed initialisation.</summary>
        public bool IsInitialized => _registry.IsInitialized;

        /// <summary>Idempotent bootstrap of the mod registry.</summary>
        public void Initialize()
        {
            _registry.Initialize();
        }

        /// <summary>Discards all loaded mods and re-runs discovery.</summary>
        public void Reload()
        {
            _registry.Reload();
        }

        /// <summary>Returns mod IDs in dependency-respecting order.</summary>
        public IReadOnlyList<string> GetOrderedModIds(bool filterDisabled = true)
            => _registry.GetOrderedModIds(filterDisabled);

        /// <summary>Looks up a mod by ID from the registry.</summary>
        public IModInfo GetMod(string id)
        {
            return _registry.GetMod(id);
        }

        /// <summary>
        /// Checks whether the given mod-relative path exists inside the
        /// specified mod's provider.
        /// </summary>
        public bool FileExists(string modId, string path)
        {
            return ProviderOf(modId)?.FileExists(path) ?? false;
        }

        /// <summary>
        /// Lists files and directories at the given mod-relative path
        /// inside the specified mod. Directories are suffixed with <c>/</c>.
        /// </summary>
        public IEnumerable<string> ListFiles(string modId, string directory = "")
        {
            // A listing sees the mod as the host sees it: its nested test suites and integrations are not its files.
            var manifest = (GetMod(modId) as ModInfo)?.Manifest;
            return NestedFolders.Visible(ProviderOf(modId), manifest)?.ListFiles(directory) ?? Enumerable.Empty<string>();
        }

        /// <summary>Reads a text file from the specified mod's provider.</summary>
        public string ReadFileText(string modId, string path)
        {
            return ProviderOf(modId)?.ReadFileText(path);
        }

        /// <summary>Reads a binary file from the specified mod's provider.</summary>
        public byte[] ReadFileBinary(string modId, string path)
        {
            return ProviderOf(modId)?.ReadFileBinary(path);
        }

        /// <summary>
        /// Opens a read-only stream to a file inside the specified mod's
        /// provider. The caller must dispose the stream.
        /// </summary>
        public Stream OpenStream(string modId, string path)
        {
            return ProviderOf(modId)?.OpenStream(path);
        }

        /// <summary>
        /// Returns the absolute filesystem path for a mod-relative path,
        /// or <c>null</c> for zip mods (which cannot provide filesystem access).
        /// </summary>
        public string GetFileSystemPath(string modId, string path)
        {
            return ProviderOf(modId)?.GetFileSystemPath(path);
        }

        /// <summary>File provider of a loaded mod, or of a mounted overlay with that id; null when neither exists.</summary>
        private IModFileProvider ProviderOf(string modId)
        {
            var mod = GetMod(modId);
            if (mod != null)
                return mod.FileProvider;
            return ModOverlays.TryGet(modId, out var overlay) ? overlay : null;
        }

        /// <summary>
        /// Reads a text file and recursively resolves all
        /// <c>${ path/to/include }</c> directives within it, returning
        /// the fully-expanded text. Circular includes are detected and
        /// reported as errors.
        /// </summary>
        public string ReadResolvedText(string modId, string path)
        {
            var provider = ProviderOf(modId);
            if (provider == null)
                return null;
            var content = provider.ReadFileText(path);
            if (content == null)
                return null;
            try
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path };
                return ExpandModIncludes(provider, content, path, visited);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS", $"Include resolution failed for '{modId}/{path}': {ex.Message}", LogLevel.Error);
                return null;
            }
        }

        /// <summary>
        /// Overload of <see cref="ReadResolvedText(string, string)"/> that
        /// uses an explicit provider instead of the mod's built-in one.
        /// Useful for resolving includes from unpacked or temporary files.
        /// </summary>
        public string ReadResolvedText(string modId, string path, IModFileProvider providerOverride)
        {
            var content = providerOverride.ReadFileText(path);
            if (content == null)
                return null;
            try
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path };
                return ExpandModIncludes(providerOverride, content, path, visited);
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS", $"Include resolution failed for '{modId}/{path}': {ex.Message}", LogLevel.Error);
                return null;
            }
        }

        /// <summary>
        /// Reads a JSON file with include resolution and deserialises it
        /// to the requested type. Returns <c>null</c> on any error.
        /// </summary>
        public T ReadJson<T>(string modId, string path) where T : class
        {
            var text = ReadResolvedText(modId, path);
            if (text == null)
                return null;
            try
            {
                return JsonSerializer.Deserialize<T>(text, JsonOptionsProvider.ReadOptions);
            }
            catch (JsonException ex)
            {
                GameLogger.Log("MODS", $"JSON deserialize failed for '{modId}/{path}': {ex.Message}", LogLevel.Error);
                return null;
            }
        }

        /// <summary>
        /// Returns a registered resolver of the specified type, or
        /// <c>null</c> if not found. Used for cross-resolver
        /// dependencies — e.g. <see cref="ShaderResolver"/> looks up
        /// <see cref="FileListResolver"/> to get the raw file list.
        /// </summary>
        internal T GetResolver<T>() where T : class, IModAssetResolver
        {
            if (_resolvers == null)
                return null;
            return _resolvers.Values.OfType<T>().FirstOrDefault();
        }

        private static readonly Regex _includePattern = new Regex(@"\$\s*\{\s*([^{}]+)\s*\}", RegexOptions.Compiled);

        /// <summary>
        /// Recursively processes <c>${ path }</c> include directives. In JSON,
        /// tokens inside strings remain literal so embedded scenario variables
        /// are substituted at execution rather than mistaken for file paths.
        /// Detects circular includes and missing files.
        /// Shared statically so that it can be used in tests without a
        /// full <see cref="ModResolutionContext"/>.
        /// </summary>
        public static string ExpandModIncludes(IModFileProvider provider, string content, string currentPath, HashSet<string> visited)
        {
            var json = string.Equals(Path.GetExtension(currentPath), ".json", StringComparison.OrdinalIgnoreCase);
            var result = new StringBuilder(content.Length);
            var previous = 0;
            var scanned = 0;
            var quoted = false;
            var escaped = false;
            foreach (Match match in _includePattern.Matches(content))
            {
                if (json)
                {
                    for (; scanned < match.Index; scanned++)
                    {
                        var character = content[scanned];
                        if (escaped) escaped = false;
                        else if (quoted && character == '\\') escaped = true;
                        else if (character == '"') quoted = !quoted;
                    }
                }
                result.Append(content, previous, match.Index - previous);
                result.Append(json && quoted ? match.Value : ExpandInclude(provider, match.Groups[1].Value.Trim(), currentPath, visited));
                previous = match.Index + match.Length;
            }
            result.Append(content, previous, content.Length - previous);
            return result.ToString();
        }

        private static string ExpandInclude(IModFileProvider provider, string includePath, string currentPath, HashSet<string> visited)
        {
            if (string.IsNullOrEmpty(includePath))
                throw new InvalidOperationException("Empty include path in ${}.");
            var resolvedPath = provider.ResolveIncludePath(includePath, currentPath);
            var loaded = provider.ReadFileText(resolvedPath);
            if (loaded == null)
            {
                resolvedPath += ".json";
                loaded = provider.ReadFileText(resolvedPath);
            }
            if (loaded == null)
                throw new InvalidOperationException($"Include not found: '{resolvedPath}' (from '${{{includePath}}}').");
            if (!visited.Add(resolvedPath))
                throw new InvalidOperationException($"Circular include detected: {resolvedPath}");
            try
            {
                return ExpandModIncludes(provider, loaded, resolvedPath, visited).Trim();
            }
            finally
            {
                visited.Remove(resolvedPath);
            }
        }
    }
}
