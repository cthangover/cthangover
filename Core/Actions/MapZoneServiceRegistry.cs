namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Kernel provider slot for <see cref="IMapZoneService"/>, the scenario
    /// facade of the world map. The map engine mod (<c>mods/map_base</c>)
    /// publishes its adapter here when it loads and clears the slot when it is
    /// unloaded; the dialog engine's action context reads
    /// <see cref="Provider"/> for <see cref="IActionContext.Map"/>. The slot
    /// is what lets <c>dialog_base</c> run without <c>map_base</c> (the novel
    /// template and the RPG have no world map): before, the dialog engine
    /// created the adapter itself and therefore had to depend on the map mod.
    /// <para/>
    /// <see cref="Provider"/> is <c>null</c> while no map mod is loaded. Only
    /// the map mod's own actions (<c>map.*</c>) use <c>ctx.Map</c>, and they
    /// exist only together with the provider, so consumers need no fallback.
    /// The adapter is stateless (the zone state lives in the mod's singleton and
    /// reaches saves through <see cref="MapStateBridge"/>), so one instance
    /// serves every context. Same shape as
    /// <c>Cthangover.Core.Quests.QuestServiceRegistry</c>.
    /// </summary>
    public static class MapZoneServiceRegistry
    {
        /// <summary>Adapter published by the map engine mod; <c>null</c> while no map mod is loaded.</summary>
        public static IMapZoneService Provider { get; set; }
    }
}
