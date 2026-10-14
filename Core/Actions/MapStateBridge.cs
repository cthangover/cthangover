using System;
using Cthangover.Core.UI.Tool;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Core-side bridge for the world map zone state. The state owner (the
    /// zone list plus runtime open/visible overrides) lives in the map_base
    /// engine mod; the kernel only touches it through these delegate slots —
    /// <c>RuntimeSerializer</c> persists the overrides into the
    /// <c>__core__</c> save slot and <c>AppServices</c> resets them when a
    /// new game starts. The mod installs its hooks through
    /// <see cref="Register"/> when the mod pipeline reaches
    /// <c>IModInitializer.OnModResourcesReady</c>. Registration is
    /// first-wins: reloading the mod re-runs its static initializers, and a
    /// second hook set must never stack on top of the first. While no hooks
    /// are bound (kernel built without the map mod, or calls before mods
    /// load) exports yield an empty delta — a save without a map mod stays
    /// loadable — and imports/resets are skipped with a warning instead of
    /// throwing, mirroring <see cref="SkillsBridge"/>.
    /// </summary>
    public static class MapStateBridge
    {
        private static readonly object Gate = new();
        private static Func<MapZoneStateData> _exportState;
        private static Action<MapZoneStateData> _importState;
        private static Action _resetOverrides;

        /// <summary>Installs the zone-state hooks; first registration wins, re-registrations are logged and ignored.</summary>
        public static void Register(Func<MapZoneStateData> exportState, Action<MapZoneStateData> importState, Action resetOverrides)
        {
            if (exportState == null || importState == null || resetOverrides == null)
                return;

            lock (Gate)
            {
                if (_exportState != null)
                {
                    GameLogger.Log("MAP", "MapStateBridge: zone-state hooks already registered; ignoring re-registration", LogLevel.Warning);
                    return;
                }

                _exportState = exportState;
                _importState = importState;
                _resetOverrides = resetOverrides;
            }
        }

        /// <summary>
        /// Copies the sparse zone overrides for the save system. Returns an
        /// empty delta (with a warning) when no map mod is bound, so saving
        /// keeps working and the slot layout stays stable.
        /// </summary>
        public static MapZoneStateData ExportState()
        {
            Func<MapZoneStateData> hook;
            lock (Gate)
                hook = _exportState;

            if (hook == null)
            {
                GameLogger.Log("MAP", "MapStateBridge: no zone-state hooks bound (map_base not loaded); exporting empty map state", LogLevel.Warning);
                return new MapZoneStateData();
            }

            return hook();
        }

        /// <summary>Replaces the overrides with the values from a loaded save; skipped with a warning when no map mod is bound.</summary>
        public static void ImportState(MapZoneStateData data)
        {
            Action<MapZoneStateData> hook;
            lock (Gate)
                hook = _importState;

            if (hook == null)
            {
                GameLogger.Log("MAP", "MapStateBridge: no zone-state hooks bound (map_base not loaded); map state restore skipped", LogLevel.Warning);
                return;
            }

            hook(data);
        }

        /// <summary>Forgets every override when a new game starts; skipped with a warning when no map mod is bound.</summary>
        public static void ResetOverrides()
        {
            Action hook;
            lock (Gate)
                hook = _resetOverrides;

            if (hook == null)
            {
                GameLogger.Log("MAP", "MapStateBridge: no zone-state hooks bound (map_base not loaded); map reset skipped", LogLevel.Warning);
                return;
            }

            hook();
        }
    }
}
