namespace Engine.Tests.Mods
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Repository-level compile guard for every mod that ships C# sources.
    /// Mod code is excluded from the main csproj and only compiled by Roslyn at
    /// runtime, so a syntax or reference error in <c>mods/*/src</c> would
    /// otherwise surface only when the game boots. The test mirrors production
    /// order (ModCompiler.CompileAll): manifest dependencies first, so
    /// mod-to-mod references resolve through the compilation cache the same
    /// way, and the real mod ids are passed to the compiler so the reference
    /// scan sees exactly the set the game would see. Mods without sources are
    /// skipped, and the whole check is skipped when the checkout has no mods.
    /// </summary>
    public class ToolsModCompileTests
    {
        [Fact]
        public void Compile_RealMods_InDependencyOrder_Succeeds()
        {
            var mods = DiscoverMods();
            if (mods.Count == 0)
                return;

            var knownIds = mods.Keys.ToList();
            foreach (var modId in TopologicalOrder(mods))
            {
                var sourceRoot = mods[modId].SourceRoot;
                if (sourceRoot == null)
                    continue;

                var files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
                if (files.Length == 0)
                    continue;

                var sources = files.Select(file => (file, File.ReadAllText(file))).ToList();

                // Compile under the REAL mod id: the cache artifact name
                // <modId>_<hash>.dll is how dependent mods resolve mod-to-mod
                // references (TryParseCacheDllName), so a "<id>_compile_test"
                // prefix would make every dependency invisible to its
                // dependents and break exactly the mods that have depends.
                var assembly = ModCompiler.Compile(
                    modId,
                    sources,
                    null,
                    default,
                    knownIds);

                if (assembly == null)
                    Assert.Fail($"Mod '{modId}' failed to compile:\n" + string.Join("\n", GameLogger.CompilationErrors));
            }
        }

        private sealed record ModGuardInfo(string Id, string SourceRoot, List<string> Depends);

        /// <summary>
        /// Finds the repository root (walks up from the test binaries until
        /// Cthangover.csproj shows up) and reads every <c>mods/*/manifest.json</c>.
        /// </summary>
        private static Dictionary<string, ModGuardInfo> DiscoverMods()
        {
            var repoRoot = FindRepoRoot();
            if (repoRoot == null)
                return new Dictionary<string, ModGuardInfo>();

            var result = new Dictionary<string, ModGuardInfo>(StringComparer.OrdinalIgnoreCase);
            var modsRoot = Path.Combine(repoRoot, "mods");
            if (!Directory.Exists(modsRoot))
                return result;

            foreach (var dir in Directory.GetDirectories(modsRoot).OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                var manifestPath = Path.Combine(dir, "manifest.json");
                if (!File.Exists(manifestPath))
                    continue;

                string id;
                var depends = new List<string>();
                try
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
                    id = document.RootElement.GetProperty("id").GetString();
                    if (document.RootElement.TryGetProperty("depends", out var dependsElement)
                        && dependsElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var dep in dependsElement.EnumerateArray())
                        {
                            var raw = dep.GetString();
                            if (string.IsNullOrEmpty(raw))
                                continue;

                            var separator = raw.IndexOfAny(new[] { '>', '=', '<' });
                            depends.Add(separator > 0 ? raw.Substring(0, separator) : raw);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Assert.Fail($"mods/{Path.GetFileName(dir)}/manifest.json is not readable: {ex.Message}");
                    return result;
                }

                var sourceRoot = Path.Combine(dir, "src");
                result[id] = new ModGuardInfo(id, Directory.Exists(sourceRoot) ? sourceRoot : null, depends);
            }

            return result;
        }

        /// <summary>
        /// Kahn's algorithm over manifest depends edges, mirroring
        /// ModCompiler.CompileAll: a dependency always precedes its dependents.
        /// </summary>
        private static List<string> TopologicalOrder(Dictionary<string, ModGuardInfo> mods)
        {
            var pending = mods.Keys.ToDictionary(
                id => id,
                id => mods[id].Depends
                    .Where(dep => mods.ContainsKey(dep))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

            var order = new List<string>();
            while (pending.Count > 0)
            {
                var ready = pending.Where(kvp => kvp.Value.Count == 0).Select(kvp => kvp.Key).ToList();
                if (ready.Count == 0)
                    Assert.Fail("Mod dependency cycle: " + string.Join(", ", pending.Keys.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)));

                foreach (var id in ready)
                {
                    order.Add(id);
                    pending.Remove(id);
                }

                foreach (var id in ready)
                    foreach (var remaining in pending)
                        remaining.Value.Remove(id);
            }

            return order;
        }

        private static string FindRepoRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "Cthangover.csproj")))
                    return directory.FullName;

                directory = directory.Parent;
            }

            return null;
        }
    }
}
