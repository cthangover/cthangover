namespace Cthangover.Tests.Mods.Pipeline
{
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Pipeline;

    /// <summary>
    /// What a mod-set reload does with each mod that has code: release it,
    /// keep its context, or load a new one — from the files the contexts run
    /// now and the files the new set compiled to.
    /// </summary>
    public class ModReloadPlanTests
    {
        private static Dictionary<string, IReadOnlyCollection<string>> Files(params (string Id, string Path)[] entries)
        {
            var map = new Dictionary<string, IReadOnlyCollection<string>>();
            foreach (var (id, path) in entries)
                map[id] = new[] { path };
            return map;
        }

        private static Dictionary<string, IReadOnlyCollection<string>> Depends(params (string Id, string[] Deps)[] entries)
        {
            var map = new Dictionary<string, IReadOnlyCollection<string>>();
            foreach (var (id, deps) in entries)
                map[id] = deps;
            return map;
        }

        [Fact]
        public void Compute_SameFiles_KeepsEveryMod()
        {
            var files = Files(("interface", "c/interface_aa.dll"), ("menu_base", "c/menu_base_bb.dll"));

            var plan = ModReloadPlan.Compute(new[] { "interface", "menu_base" }, files, new[] { "interface", "menu_base" }, files, Depends(), null);

            Assert.Empty(plan.Unload);
            Assert.Empty(plan.Load);
            Assert.Equal(new[] { "interface", "menu_base" }, plan.Keep);
        }

        [Fact]
        public void Compute_ChangedFile_ReloadsThatMod()
        {
            var plan = ModReloadPlan.Compute(
                new[] { "interface", "tools" },
                Files(("interface", "c/interface_aa.dll"), ("tools", "c/tools_old.dll")),
                new[] { "interface", "tools" },
                Files(("interface", "c/interface_aa.dll"), ("tools", "c/tools_new.dll")),
                Depends(("tools", new[] { "interface" })),
                null);

            Assert.Equal(new[] { "tools" }, plan.Unload);
            Assert.Equal(new[] { "tools" }, plan.Load);
            Assert.Equal(new[] { "interface" }, plan.Keep);
        }

        [Fact]
        public void Compute_ChangedDependency_ReloadsTransitiveDependents_DependentsFirst()
        {
            var plan = ModReloadPlan.Compute(
                new[] { "interface", "shell2d_base", "dialog_base", "menu_base" },
                Files(("interface", "c/i1.dll"), ("shell2d_base", "c/b.dll"), ("dialog_base", "c/d.dll"), ("menu_base", "c/m.dll")),
                new[] { "interface", "shell2d_base", "dialog_base", "menu_base" },
                Files(("interface", "c/i2.dll"), ("shell2d_base", "c/b.dll"), ("dialog_base", "c/d.dll"), ("menu_base", "c/m.dll")),
                Depends(("shell2d_base", new[] { "interface" }), ("dialog_base", new[] { "shell2d_base" }), ("menu_base", new string[0])),
                null);

            Assert.Equal(new[] { "dialog_base", "shell2d_base", "interface" }, plan.Unload);
            Assert.Equal(new[] { "interface", "shell2d_base", "dialog_base" }, plan.Load);
            Assert.Equal(new[] { "menu_base" }, plan.Keep);
        }

        [Fact]
        public void Compute_ModLeavingTheSet_IsUnloadedOnly()
        {
            var plan = ModReloadPlan.Compute(
                new[] { "interface", "ff_battle" },
                Files(("interface", "c/i.dll"), ("ff_battle", "c/ff.dll")),
                new[] { "interface" },
                Files(("interface", "c/i.dll")),
                Depends(),
                null);

            Assert.Equal(new[] { "ff_battle" }, plan.Unload);
            Assert.Empty(plan.Load);
            Assert.Equal(new[] { "interface" }, plan.Keep);
        }

        [Fact]
        public void Compute_ModEnteringTheSet_IsLoadedOnly()
        {
            var plan = ModReloadPlan.Compute(
                new[] { "interface" },
                Files(("interface", "c/i.dll")),
                new[] { "interface", "cooking" },
                Files(("interface", "c/i.dll"), ("cooking", "c/cooking.dll")),
                Depends(("cooking", new[] { "interface" })),
                null);

            Assert.Empty(plan.Unload);
            Assert.Equal(new[] { "cooking" }, plan.Load);
            Assert.Equal(new[] { "interface" }, plan.Keep);
        }

        [Fact]
        public void Compute_ForcedMod_ReloadsEvenUnchanged()
        {
            var files = Files(("interface", "c/i.dll"), ("cthangover_party", "c/s.dll"));

            var plan = ModReloadPlan.Compute(new[] { "interface", "cthangover_party" }, files, new[] { "interface", "cthangover_party" }, files, Depends(), new[] { "CTHANGOVER_PARTY" });

            Assert.Equal(new[] { "cthangover_party" }, plan.Unload);
            Assert.Equal(new[] { "cthangover_party" }, plan.Load);
            Assert.Equal(new[] { "interface" }, plan.Keep);
        }

        [Fact]
        public void Compute_ForceAll_ReloadsEveryLoadedMod()
        {
            var files = Files(("interface", "c/i.dll"), ("tools", "c/t.dll"));

            var plan = ModReloadPlan.Compute(new[] { "interface", "tools" }, files, new[] { "interface", "tools" }, files, Depends(), new[] { "*" });

            Assert.Equal(new[] { "tools", "interface" }, plan.Unload);
            Assert.Equal(new[] { "interface", "tools" }, plan.Load);
            Assert.Empty(plan.Keep);
        }

        [Fact]
        public void Compute_PathSeparatorsAndIdCase_DoNotCountAsChanges()
        {
            var plan = ModReloadPlan.Compute(
                new[] { "Interface" },
                Files(("interface", @"c:\cache\interface_aa.dll")),
                new[] { "interface" },
                Files(("INTERFACE", "c:/cache/interface_aa.dll")),
                Depends(),
                null);

            Assert.Empty(plan.Unload);
            Assert.Empty(plan.Load);
            Assert.Single(plan.Keep);
        }

        [Fact]
        public void Compute_NothingLoadedNothingTargeted_IsEmpty()
        {
            var plan = ModReloadPlan.Compute(null, null, null, null, null, null);

            Assert.Empty(plan.Unload);
            Assert.Empty(plan.Keep);
            Assert.Empty(plan.Load);
        }
    }
}
