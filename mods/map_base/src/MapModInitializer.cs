using Cthangover.Core.Actions;
using Cthangover.Core.Mods;

namespace Cthangover.Mods.Map
{
    /// <summary>
    /// Wires the map subsystem into the kernel. On
    /// <c>OnModResourcesReady</c> it installs the zone-state hooks into
    /// <see cref="MapStateBridge"/> — export into the save's
    /// <c>__core__</c> slot, restore from a loaded save, and the
    /// new-game override reset — so the core save pipeline keeps working
    /// against the mod-owned <see cref="MapZoneService"/> singleton.
    /// </summary>
    public class MapModInitializer : IModInitializer
    {
        /// <summary>No per-mod work: the bridge binding happens once the whole mod set is ready.</summary>
        public void OnModLoaded(string modId)
        {
        }

        /// <summary>Installs the zone-state hooks (first registration wins).</summary>
        public void OnModResourcesReady()
        {
            var service = MapZoneService.Instance;
            // Runs again after every mod reload; the service outlives it when map_base stays loaded.
            service.MarkStale();
            MapStateBridge.Register(service.ExportState, service.ImportState, service.ResetOverrides);
        }
    }
}
