namespace Cthangover.Core.Mods.Resolvers
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using Cthangover.Core.Scenarios;
    using Cthangover.Core.Utils;

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
        {
            return _registry.GetOrderedModIds(filterDisabled);
        }

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
        /// Reads through the selected provider and expands includes. The root scenario may opt into
        /// <c>include_syntax: explicit-v1</c>: only <c>$include{path}</c> expands in non-JSON fragments,
        /// preserving runtime <c>${name}</c> independently of files. Other roots retain legacy syntax.
        /// </summary>
        public string ReadResolvedText(string modId, string path)
        {
            return ReadResolvedSource(modId, path)?.ExpandedText;
        }

        /// <summary>
        /// Overload of <see cref="ReadResolvedText(string, string)"/> that
        /// uses an explicit provider instead of the mod's built-in one.
        /// Useful for resolving includes from unpacked or temporary files.
        /// </summary>
        public string ReadResolvedText(string modId, string path, IModFileProvider providerOverride)
        {
            return ReadResolvedSource(modId, path, providerOverride)?.ExpandedText;
        }

        /// <summary>
        /// Captures authored root text and the exact expansion/provenance from one selected provider.
        /// A provider override never falls back to another mod; include failures return null as in expanded reads.
        /// </summary>
        public ResolvedModText ReadResolvedSource(string modId, string path, IModFileProvider providerOverride = null)
        {
            var provider = providerOverride ?? ProviderOf(modId);
            var content = provider?.ReadFileText(path);
            if (content == null)
                return null;
            try
            {
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path };
                var inputs = new StringBuilder();
                var expanded = ExpandModIncludes(provider, content, path, visited,
                    UsesExplicitScenarioIncludes(content, path), inputs, 0, 0);
                return new ResolvedModText(provider, ModPath.Normalize(path), content, expanded.Text, expanded.Origins,
                    expanded.RootIncludeLines,
                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(inputs.ToString()))));
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

        private static readonly Regex explicitIncludePattern = new(@"\$include\{\s*([^{}]*)\s*\}", RegexOptions.Compiled);

        /// <summary>
        /// Selects syntax once from a root scenario's existing metadata grammar, then propagates it through
        /// provider-relative fragments. JSON keeps legacy includes outside strings and literal tokens inside
        /// strings. Expansion never rescans caller/runtime substitutions; missing files and cycles refuse.
        /// </summary>
        public static string ExpandModIncludes(IModFileProvider provider, string content, string currentPath, HashSet<string> visited)
        {
            return ExpandModIncludes(provider, content, currentPath, visited,
                UsesExplicitScenarioIncludes(content, currentPath), new StringBuilder(), 0, 0).Text;
        }

        private static bool UsesExplicitScenarioIncludes(string content, string path)
        {
            if (!string.Equals(Path.GetExtension(path), ".scenario", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var (metadata, _) = ScenarioMetadata.ParseMetadata(content);
            if (!metadata.TryGetValue("include_syntax", out var syntax))
            {
                return false;
            }

            if (syntax != "explicit-v1")
            {
                throw new InvalidOperationException($"Unsupported scenario include_syntax '{syntax}' in '{path}'.");
            }

            return true;
        }

        private static ModTextBuilder ExpandModIncludes(IModFileProvider provider, string content, string currentPath,
            HashSet<string> visited, bool explicitSyntax, StringBuilder inputs, int rootLine, int rootIncludeOffset)
        {
            var canonicalPath = ModPath.Normalize(currentPath);
            inputs.Append(canonicalPath.Length).Append(':').Append(canonicalPath)
                .Append(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))));
            var json = string.Equals(Path.GetExtension(currentPath), ".json", StringComparison.OrdinalIgnoreCase);
            var pattern = explicitSyntax && !json ? explicitIncludePattern : _includePattern;
            var result = new ModTextBuilder();
            var previous = 0;
            var sourceLine = 1;
            var scanned = 0;
            var quoted = false;
            var escaped = false;
            foreach (Match match in pattern.Matches(content))
            {
                if (json)
                {
                    for (; scanned < match.Index; scanned++)
                    {
                        var character = content[scanned];
                        if (escaped)
                        {
                            escaped = false;
                        }
                        else if (quoted && character == '\\')
                        {
                            escaped = true;
                        }
                        else if (character == '"')
                        {
                            quoted = !quoted;
                        }
                    }
                }
                sourceLine = result.Append(content, previous, match.Index - previous, currentPath,
                    sourceLine, rootLine, rootIncludeOffset);
                if (json && quoted)
                {
                    sourceLine = result.Append(content, match.Index, match.Length, currentPath,
                        sourceLine, rootLine, rootIncludeOffset);
                }
                else
                {
                    if (rootIncludeOffset == 0)
                    {
                        result.AddRootInclude(sourceLine);
                    }

                    result.AppendTrimmed(ExpandInclude(provider, match.Groups[1].Value.Trim(), currentPath, visited,
                        explicitSyntax, inputs, rootIncludeOffset == 0 ? sourceLine : rootLine,
                        rootIncludeOffset == 0 ? match.Index + 1 : rootIncludeOffset));
                    foreach (var character in match.Value)
                    {
                        if (character == '\n')
                        {
                            sourceLine++;
                        }
                    }
                }

                previous = match.Index + match.Length;
            }
            result.Append(content, previous, content.Length - previous, currentPath, sourceLine, rootLine, rootIncludeOffset);
            return result;
        }

        private static ModTextBuilder ExpandInclude(IModFileProvider provider, string includePath, string currentPath,
            HashSet<string> visited, bool explicitSyntax, StringBuilder inputs, int rootLine, int rootIncludeOffset)
        {
            var directive = explicitSyntax && !string.Equals(Path.GetExtension(currentPath), ".json", StringComparison.OrdinalIgnoreCase)
                ? "$include{" : "${";
            if (string.IsNullOrEmpty(includePath))
            {
                throw new InvalidOperationException("Empty include path in " + directive + "}.");
            }
            var resolvedPath = provider.ResolveIncludePath(includePath, currentPath);
            var loaded = provider.ReadFileText(resolvedPath);
            if (loaded == null)
            {
                resolvedPath += ".json";
                loaded = provider.ReadFileText(resolvedPath);
            }
            if (loaded == null)
            {
                throw new InvalidOperationException($"Include not found: '{resolvedPath}' (from '{directive}{includePath}}}').");
            }
            if (!visited.Add(resolvedPath))
            {
                throw new InvalidOperationException($"Circular include detected: {resolvedPath}");
            }
            try
            {
                return ExpandModIncludes(provider, loaded, resolvedPath, visited, explicitSyntax, inputs, rootLine, rootIncludeOffset);
            }
            finally
            {
                visited.Remove(resolvedPath);
            }
        }
    }
}
