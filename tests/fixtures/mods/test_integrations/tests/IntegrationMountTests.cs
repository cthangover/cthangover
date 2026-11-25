namespace Cthangover.Mods.TestIntegrations.Tests
{
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Games;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Utils;

    /// <summary>
    /// The integration <c>test_integrations__probe</c> (partner <c>quests_base</c>) joins a mod set exactly when
    /// the partner is in it — at boot (the running set) and when <c>ModSetReloader</c> prepares an
    /// in-process switch. Run with <c>--with-mods=test_integrations</c>, once with all mods and once
    /// with <c>--frontend</c> (no quests): the boot case asserts whichever set it got.
    /// </summary>
    public class IntegrationMountTests
    {
        private const string Probe = "test_integrations__probe";

        [ModTest("integrations/boot_mounts_only_with_the_partner")]
        static void BootMountsOnlyWithThePartner(ModTestContext ctx)
        {
            var partner = ModRegistry.Instance.GetMod("quests_base") != null;
            var probe = ModRegistry.Instance.GetMod(Probe) as ModInfo;

            ModAssert.True(partner == (probe != null), partner ? "mounted with quests_base loaded" : "left out without quests_base");
            if (probe == null)
                return;
            ModAssert.True(probe.Manifest.DependencyIds.Contains("test_integrations"), "depends on its host");
            ModAssert.True(probe.Manifest.DependencyIds.Contains("quests_base"), "depends on its partner");
            ModAssert.True(ModRegistry.Instance.AllDiscoveredMods.ContainsKey("test_integrations"), "the host is an ordinary mod");
            ModAssert.False(ModRegistry.Instance.AllDiscoveredMods.ContainsKey(Probe), "the integration has no card of its own");
            ModAssert.NotNull(System.AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Cthangover.Mods.TestIntegrations.IntegrationProbe"))
                .FirstOrDefault(t => t != null), "the glue compiled against the partner and loaded");
        }

        [ModTest("integrations/reloader_mounts_only_with_the_partner")]
        static void ReloaderMountsOnlyWithThePartner(ModTestContext ctx)
        {
            ModAssert.False(Prepared("--frontend", "--with-mods=test_integrations").Contains(Probe), "a switch to a set without quests leaves it out");
            ModAssert.True(Prepared("--frontend", "--with-mods=test_integrations,quests_base").Contains(Probe), "a switch to a set with quests mounts it");
        }

        /// <summary>The mod ids an in-process switch to <paramref name="args"/> would load; nothing running changes.</summary>
        private static HashSet<string> Prepared(params string[] args)
        {
            var reloader = ModRegistry.Instance.Reloader;
            var errors = new List<string>(GameLogger.CompilationErrors);
            var prep = reloader.Prepare(GameLaunchArgs.Parse(args), null);
            try
            {
                ModAssert.True(prep.Succeeded, $"the set compiles: {string.Join("; ", prep.Errors)}");
                ModAssert.True(prep.ModIds.Contains("test_integrations"), "the host is in the set");
                return new HashSet<string>(prep.ModIds);
            }
            finally
            {
                reloader.Discard(prep);
                GameLogger.CompilationErrors.Clear();
                GameLogger.CompilationErrors.AddRange(errors);
            }
        }
    }
}
