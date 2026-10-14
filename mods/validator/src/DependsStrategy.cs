using System;
using System.Collections.Generic;
using Cthangover.Core.Mods;

namespace Cthangover.Validator
{
    /// <summary>
    /// Phase: Reconciliation. Checks manifest <c>depends</c> entries: malformed
    /// strings, required dependencies missing from the loaded set, and circular
    /// dependency chains. The registry already filters mods with broken
    /// dependencies at load time and the compiler only logs a warning for
    /// cycles, so this strategy is what turns those silent conditions into
    /// reportable findings.
    /// </summary>
    public class DependsStrategy : IValidationStrategy
    {
        public string Id => "depends";
        public ValidationPhase Phase => ValidationPhase.Reconciliation;
        public int Order => 100;
        public ValidationFeature Feature => ValidationFeature.Json;

        /// <summary>Re-checks every manifest's depends pins and cycles, reporting broken chains the loader only logs at runtime.</summary>
        public void Validate(ValidationContext context)
        {
            var manifests = new Dictionary<string, ModManifest>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in context.Mods)
            {
                context.ThrowIfCancellationRequested();

                var manifest = (mod as ModInfo)?.Manifest;
                if (manifest != null)
                    manifests[mod.Id] = manifest;
            }

            foreach (var entry in manifests)
            {
                var depends = entry.Value.Depends;
                if (depends == null)
                    continue;

                foreach (var dependency in depends)
                {
                    if (!dependency.IsValid)
                    {
                        context.AddError("SCHEMA_WRONG_TYPE",
                            $"invalid dependency string: {dependency.Error}",
                            entry.Key, "manifest.json", 0,
                            "use format modId[op][version][?]");
                        continue;
                    }

                    if (dependency.IsOptional || manifests.ContainsKey(dependency.ModId))
                        continue;

                    context.AddError("REF_NOT_FOUND",
                        $"required dependency '{dependency.ModId}' is not loaded",
                        entry.Key, "manifest.json", 0,
                        $"install mod '{dependency.ModId}' or mark the dependency optional with '?'");
                }
            }

            DetectCycles(context, manifests);
        }

        private static void DetectCycles(ValidationContext context, Dictionary<string, ModManifest> manifests)
        {
            var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var path = new List<string>();
            var reported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var modId in manifests.Keys)
                Visit(context, manifests, modId, state, path, reported);
        }

        private static void Visit(
            ValidationContext context,
            Dictionary<string, ModManifest> manifests,
            string modId,
            Dictionary<string, int> state,
            List<string> path,
            HashSet<string> reported)
        {
            if (state.TryGetValue(modId, out var existing))
            {
                if (existing == 1)
                    ReportCycle(context, modId, path, reported);
                return;
            }

            state[modId] = 1;
            path.Add(modId);

            var depends = manifests[modId].Depends;
            if (depends != null)
            {
                foreach (var dependency in depends)
                {
                    if (!dependency.IsValid || !manifests.ContainsKey(dependency.ModId))
                        continue;

                    Visit(context, manifests, dependency.ModId, state, path, reported);
                }
            }

            path.RemoveAt(path.Count - 1);
            state[modId] = 2;
        }

        private static void ReportCycle(ValidationContext context, string repeated, List<string> path, HashSet<string> reported)
        {
            var start = -1;
            for (var i = 0; i < path.Count; i++)
            {
                if (string.Equals(path[i], repeated, StringComparison.OrdinalIgnoreCase))
                {
                    start = i;
                    break;
                }
            }

            if (start < 0)
                return;

            var cycle = new List<string>();
            for (var i = start; i < path.Count; i++)
                cycle.Add(path[i]);
            cycle.Add(repeated);

            var key = BuildCycleKey(cycle);
            if (!reported.Add(key))
                return;

            context.AddError("REF_CIRCULAR",
                $"circular dependency: {string.Join(" -> ", cycle)}",
                repeated, "manifest.json", 0,
                "break the cycle by removing one of the dependencies");
        }

        private static string BuildCycleKey(List<string> cycle)
        {
            var sorted = new List<string>(cycle);
            sorted.Sort(StringComparer.OrdinalIgnoreCase);
            return string.Join("|", sorted);
        }
    }
}
