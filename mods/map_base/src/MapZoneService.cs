using System;
using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Events;
using Cthangover.Core.UI.Tool;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;

namespace Cthangover.Mods.Map
{
    /// <summary>
    /// Single owner of the world map zone list and its live state. Replaces the
    /// raw "read JSON per mod" logic that used to live inside <see cref="MapWidget"/>
    /// so that the map, scenarios and mods all see the same zones and the same
    /// open/hidden flags.
    /// <para/>
    /// Two layers of state exist:
    /// <list type="bullet">
    /// <item>authoring data — <see cref="MapZone"/> loaded from
    /// <c>mods/&lt;name&gt;/map/*.json</c>, with initial <c>locked</c>/<c>hidden</c> flags;</item>
    /// <item>runtime overrides — sparse dictionaries managed here through
    /// <see cref="Open"/>, <see cref="Close"/>, <see cref="Show"/>, <see cref="Hide"/>
    /// and <see cref="ResetZone"/>.</item>
    /// </list>
    /// The effective state is the override when present, otherwise the authoring
    /// default. Overrides are what <see cref="ExportState"/> writes into the save
    /// and <see cref="ImportState"/> restores, so a mod update that changes the
    /// defaults is immediately reflected for every zone the player never touched.
    /// <para/>
    /// Every effective state change publishes <see cref="MapZoneChangedEvent"/> —
    /// <see cref="MapWidget"/> rebuilds its layers from it and
    /// <see cref="Scenes.ConditionStateMonitor"/> invalidates cached scenario
    /// conditions, because zone state can gate scenario headers.
    /// <para/>
    /// The single instance is process-wide and deliberately not owned by
    /// <see cref="AppServices"/>: zone overrides are identity-sensitive across
    /// the whole process (map widget, saves, scenario conditions). New-game
    /// reset goes through the AppServices resettable adapter ("map_zones",
    /// see <see cref="ResetOverrides"/>); unit tests inject zones via
    /// <see cref="LoadZones"/> while <c>GameEnvironment.IsRuntime</c> is false.
    /// </summary>
    public sealed class MapZoneService
    {
        /// <summary>Owns the process-wide instance; lazily created and alive for the whole session.</summary>
        private static readonly Lazy<MapZoneService> instance = new(() => new MapZoneService());

    /// <summary>Process-wide singleton. Deliberately NOT owned by AppServices:
    /// zone state is identity-sensitive across the process (map widget,
    /// saves, scenario conditions); reset goes through the AppServices
    /// resettable adapter instead.</summary>
    public static MapZoneService Instance => instance.Value;

        private readonly List<MapZone> zones = new();
        private readonly Dictionary<string, MapZone> byId = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> openOverrides = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, bool> visibleOverrides = new(StringComparer.OrdinalIgnoreCase);
        private bool loaded;

        private MapZoneService() { }

        /// <summary>
        /// All loaded zones in mod load order, hidden ones included. The map
        /// filters by <see cref="IsVisible"/>; other consumers (e.g. the map
        /// editor) may want the full list.
        /// </summary>
        public IReadOnlyList<MapZone> Zones => zones;

        /// <summary>
        /// Loads the zone list from the given data, replacing the previous list
        /// but preserving runtime overrides. This is the testable entry point;
        /// runtime code uses <see cref="ReloadFromMods"/>.
        /// </summary>
        public void LoadZones(IEnumerable<MapZone> newZones)
        {
            zones.Clear();
            byId.Clear();

            if (newZones != null)
            {
                foreach (var zone in newZones)
                {
                    if (zone == null || string.IsNullOrEmpty(zone.ID))
                        continue;

                    if (byId.ContainsKey(zone.ID))
                    {
                        GameLogger.Log("MAP", $"duplicate zone id '{zone.ID}' ignored", LogLevel.Warning);
                        continue;
                    }

                    zones.Add(zone);
                    byId[zone.ID] = zone;
                }
            }

            loaded = true;
            GameLogger.Log("MAP", $"MapZoneService loaded {zones.Count} zone(s)");
        }

        /// <summary>
        /// Reads every loaded mod's <c>map/*.json</c> through its
        /// <see cref="IModFileProvider"/> and rebuilds the zone list. Safe to
        /// call outside the Godot runtime: the call is a no-op in unit tests,
        /// where zones are injected with <see cref="LoadZones"/> instead.
        /// </summary>
        public void ReloadFromMods()
        {
            if (!GameEnvironment.IsRuntime)
            {
                loaded = true;
                return;
            }

            var result = new List<MapZone>();
            var options = JsonOptionsProvider.ReadOptions;

            foreach (var kvp in ModManager.Instance.Mods)
            {
                var modId = kvp.Key;
                var provider = kvp.Value.FileProvider;

                foreach (var entry in provider.ListFiles("map"))
                {
                    if (entry.EndsWith("/") || !entry.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var jsonText = provider.ReadFileText(entry);
                    if (jsonText == null)
                    {
                        GameLogger.Log("MAP", $"failed to read map json '{modId}/{entry}'", LogLevel.Error);
                        continue;
                    }

                    try
                    {
                        var fileData = JsonSerializer.Deserialize<FileData<MapZone>>(jsonText, options);
                        if (fileData?.Items == null)
                            continue;

                        foreach (var zone in fileData.Items)
                        {
                            result.Add(zone);
                            GameLogger.Log("MAP", $"loaded zone '{zone.ID}' from '{modId}'");
                        }
                    }
                    catch (JsonException ex)
                    {
                        GameLogger.Log("MAP", $"JSON parse failed for '{modId}/{entry}': {ex.Message}", LogLevel.Error);
                    }
                }
            }

            LoadZones(result);
        }

        /// <summary>
        /// Loads zones on first use and re-reads nothing afterwards; call
        /// <see cref="Reload"/> when the mod set or the map files changed.
        /// </summary>
        public void EnsureLoaded()
        {
            if (!loaded)
                ReloadFromMods();
        }

        /// <summary>
        /// Re-reads the raw zone data while keeping every runtime override, so a
        /// running game picks up map edits without losing progression. Publishes
        /// a zone change so the map rebuilds.
        /// </summary>
        public void Reload()
        {
            ReloadFromMods();
            ModEventBus.Publish(new MapZoneChangedEvent { ZoneId = string.Empty, Change = MapZoneChangeKind.Reload });
        }

        /// <summary>Returns the zone with the given ID, or <c>null</c> when unknown.</summary>
        public MapZone Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return byId.TryGetValue(id, out var zone) ? zone : null;
        }

        /// <summary>True when a zone with the given ID is loaded from any mod.</summary>
        public bool Contains(string id) => Get(id) != null;

        /// <summary>
        /// Effective open state: runtime override when present, otherwise the
        /// authoring default (<see cref="MapZone.Locked"/> negated). Unknown IDs
        /// are closed. Note that a zone is only actually enterable when its
        /// scene also resolves — that check stays in <see cref="MapWidget"/>.
        /// </summary>
        public bool IsOpen(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            if (openOverrides.TryGetValue(id, out var value))
                return value;
            return Get(id) is { Locked: false };
        }

        /// <summary>
        /// Effective visibility: runtime override when present, otherwise the
        /// authoring default (<see cref="MapZone.Hidden"/> negated). Unknown IDs
        /// are invisible.
        /// </summary>
        public bool IsVisible(string id)
        {
            if (string.IsNullOrEmpty(id))
                return false;
            if (visibleOverrides.TryGetValue(id, out var value))
                return value;
            return Get(id) is { Hidden: false };
        }

        /// <summary>Opens the zone for travel, overriding the authoring default.</summary>
        public void Open(string id) => SetOpen(id, true);

        /// <summary>Closes the zone for travel, overriding the authoring default.</summary>
        public void Close(string id) => SetOpen(id, false);

        /// <summary>Reveals a hidden zone on the map.</summary>
        public void Show(string id) => SetVisible(id, true);

        /// <summary>Removes the zone from the map entirely until shown again.</summary>
        public void Hide(string id) => SetVisible(id, false);

        /// <summary>
        /// Drops both overrides, returning the zone to its authoring defaults.
        /// Unlike the other mutators this one works on unknown IDs too, so stale
        /// overrides of removed zones can be cleaned up.
        /// </summary>
        public void ResetZone(string id)
        {
            if (string.IsNullOrEmpty(id))
                return;

            var changed = openOverrides.Remove(id);
            changed |= visibleOverrides.Remove(id);
            if (changed)
                PublishChange(id, MapZoneChangeKind.Reset);
        }

        /// <summary>
        /// Forgets every override, returning all zones to their authoring
        /// defaults. Called when a new game starts so progression from the
        /// previous playthrough does not leak into it.
        /// </summary>
        public void ResetOverrides()
        {
            if (openOverrides.Count == 0 && visibleOverrides.Count == 0)
                return;

            openOverrides.Clear();
            visibleOverrides.Clear();
            PublishChange(string.Empty, MapZoneChangeKind.Reset);
        }

        /// <summary>Copies the sparse overrides for the save system.</summary>
        public MapZoneStateData ExportState()
        {
            return new MapZoneStateData
            {
                Open = new Dictionary<string, bool>(openOverrides, StringComparer.OrdinalIgnoreCase),
                Visible = new Dictionary<string, bool>(visibleOverrides, StringComparer.OrdinalIgnoreCase),
            };
        }

        /// <summary>
        /// Replaces the overrides with the values from a save. A save is the
        /// authoritative override set of its own playthrough, so the previous
        /// overrides are dropped entirely; zone IDs that no longer exist are
        /// kept (they cost nothing and survive a temporarily disabled mod).
        /// </summary>
        public void ImportState(MapZoneStateData data)
        {
            openOverrides.Clear();
            visibleOverrides.Clear();

            if (data?.Open != null)
            {
                foreach (var pair in data.Open)
                    openOverrides[pair.Key] = pair.Value;
            }

            if (data?.Visible != null)
            {
                foreach (var pair in data.Visible)
                    visibleOverrides[pair.Key] = pair.Value;
            }

            PublishChange(string.Empty, MapZoneChangeKind.Restore);
        }

        private void SetOpen(string id, bool value)
        {
            if (!ChangeOverride(openOverrides, id, value, "open/close"))
                return;
            PublishChange(id, value ? MapZoneChangeKind.Open : MapZoneChangeKind.Close);
        }

        private void SetVisible(string id, bool value)
        {
            if (!ChangeOverride(visibleOverrides, id, value, "show/hide"))
                return;
            PublishChange(id, value ? MapZoneChangeKind.Show : MapZoneChangeKind.Hide);
        }

        private bool ChangeOverride(Dictionary<string, bool> overrides, string id, bool value, string operation)
        {
            if (string.IsNullOrEmpty(id))
                return false;

            if (!Contains(id))
            {
                GameLogger.Log("MAP", $"cannot {operation} unknown zone '{id}'", LogLevel.Warning);
                return false;
            }

            if (overrides.TryGetValue(id, out var current) && current == value)
                return false;

            overrides[id] = value;
            return true;
        }

        private static void PublishChange(string zoneId, string change)
        {
            ModEventBus.Publish(new MapZoneChangedEvent { ZoneId = zoneId, Change = change });
        }
    }
}
