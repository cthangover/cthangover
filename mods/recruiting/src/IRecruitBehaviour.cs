using Cthangover.Core.Settings;

namespace Cthangover.Core.Relationship
{
    /// <summary>
    /// Per-recruit behaviour plugin discovered via reflection from mod
    /// assemblies. Each implementation is registered into
    /// <see cref="RecruitBehaviourRegistry"/> by
    /// <see cref="Mods.ModAssemblyLoader.RegisterAssembly"/> and then
    /// invoked for <b>every</b> active recruit — not just those from the
    /// owning mod — during tick, configure, and removal lifecycle events.
    ///
    /// This cross-recruit dispatch pattern is intentional: a behaviour
    /// might want to introspect all recruits (e.g. a "rivalry" system
    /// that checks for conflicting personalities), so the registry passes
    /// the full recruit list to every behaviour rather than filtering by
    /// ownership.
    /// </summary>
    public interface IRecruitBehaviour
    {
        /// <summary>Unique identifier for lookup via <c>RecruitBehaviourRegistry.Get</c>.</summary>
        string Id { get; }

        /// <summary>
        /// Called once when a recruit is added to the party
        /// (via <c>RecruitingData.Add</c>). Use for initial stat
        /// setup, buff application, or spawning companion nodes.
        /// </summary>
        void ConfigureRecruit(Recruit recruit, RuntimeData runtime);

        /// <summary>
        /// Called every game tick for each active recruit. The
        /// <paramref name="currentTick"/> value is the global
        /// <c>RuntimeData.Time.Tick</c> counter — use it for
        /// cooldown-based or periodic logic rather than maintaining
        /// a private timer.
        /// </summary>
        void OnTick(Recruit recruit, RuntimeData runtime, long currentTick);

        /// <summary>
        /// Called when a recruit is removed from the party
        /// (via <c>RecruitingData.Remove</c>). Clean up any
        /// persisted state, buffs, or companion nodes.
        /// </summary>
        void OnRemove(Recruit recruit, RuntimeData runtime);
    }

}
