namespace Cthangover.Core.Mods
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Mounts integrations (<see cref="ModManifest.Integrations"/>): glue folders inside a host mod that
    /// join a mod set only when the host and every partner are in it — battles tag a quest on victory
    /// only in a game that has quests, and turning quests off just leaves the glue out instead of
    /// breaking battles (<c>docs/analysis/dependency-cleanup.md</c>).
    /// <para>
    /// The gate is the final set: <see cref="Mount"/> runs after the disabled mods and the game's
    /// collection filtered the discovered mods, and before dependencies are validated — at boot
    /// (<c>ModBootPipeline</c>) and on an in-process game switch (<c>ModSetReloader</c>) alike. An
    /// integration becomes the virtual mod <c>&lt;host&gt;__&lt;id&gt;</c> (like a test suite) that depends on
    /// its host and its partners, so it compiles against both and loads after them. It is never part
    /// of a collection, the disabled list or the mod manager's cards, so a player cannot toggle it, and
    /// it holds glue only — no content, no save data.
    /// </para>
    /// </summary>
    public static class ModIntegrations
    {
        /// <summary>Default parent folder of integrations inside the host mod.</summary>
        public const string FolderName = "integrations";

        /// <summary>
        /// True when <paramref name="integration"/> belongs in a set of <paramref name="modIds"/> (case-insensitive)
        /// that contains its host: it has an id and every partner is in the set. Pure.
        /// </summary>
        public static bool IsWanted(ModIntegration integration, IEnumerable<string> modIds)
        {
            if (integration == null || string.IsNullOrWhiteSpace(integration.Id))
                return false;
            var set = new HashSet<string>(modIds ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            return (integration.With ?? new List<string>()).All(partner => !string.IsNullOrWhiteSpace(partner) && set.Contains(partner.Trim()));
        }

        /// <summary>
        /// Adds the wanted integrations of the hosts in <paramref name="mods"/> (the filtered set) as
        /// virtual mods and returns their ids (no state is kept: a background warm-up prepares sets that
        /// are never applied). A missing folder or manifest, or an id clash with an installed mod, is an
        /// ERROR and the integration is left out.
        /// </summary>
        public static List<string> Mount(IDictionary<string, IModInfo> mods)
        {
            var mounted = new List<string>();
            var present = mods.Keys.ToList();
            foreach (var host in mods.Values.OfType<ModInfo>().ToList())
            {
                foreach (var integration in host.Manifest?.Integrations ?? new List<ModIntegration>())
                {
                    var id = ModTestSuites.ModIdOf(host.Id, integration?.Id);
                    if (!IsWanted(integration, present))
                    {
                        GameLogger.Log("MODS_REGISTRY", $"Integration '{id}' left out: needs {string.Join(", ", integration?.With ?? new List<string>())}");
                        continue;
                    }

                    var info = NestedMod.Create(host, id, integration.Folder, "Integration",
                        $"{host.Name ?? host.Id} + {string.Join(", ", integration.With ?? new List<string>())}");
                    if (info == null)
                        continue;
                    if (mods.ContainsKey(info.Id))
                    {
                        GameLogger.Log("MODS_REGISTRY", $"Integration '{info.Id}' clashes with an installed mod id; skipped", LogLevel.Error);
                        info.FileProvider.Dispose();
                        continue;
                    }

                    foreach (var partner in integration.With ?? new List<string>())
                    {
                        if (info.Manifest.Depends.All(d => !string.Equals(d.ModId, partner.Trim(), StringComparison.OrdinalIgnoreCase)))
                            info.Manifest.Depends.Add(ModDependency.Parse(partner.Trim()));
                    }

                    mods[info.Id] = info;
                    mounted.Add(info.Id);
                    GameLogger.Log("MODS_REGISTRY", $"Integration '{info.Id}' mounted from '{host.Id}/{integration.Folder}'");
                }
            }
            return mounted;
        }
    }
}
