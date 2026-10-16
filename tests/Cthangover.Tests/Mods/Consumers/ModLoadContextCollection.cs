namespace Cthangover.Tests.Mods.Consumers
{
    /// <summary>
    /// Non-parallel collection for collectible-ALC tests. The unload-verification
    /// phase does forced GC cycles; concurrent test threads (other collections
    /// allocating, JIT-compiling, holding stack frames into managed code) make
    /// the weak-reference collection check flaky, so these tests run alone.
    /// Same scheme as the existing "appservices" and "ConditionStateMonitor"
    /// collections.
    /// </summary>
    [Xunit.CollectionDefinition("mod-load-context", DisableParallelization = true)]
    public class ModLoadContextCollectionDefinition
    {
    }
}
