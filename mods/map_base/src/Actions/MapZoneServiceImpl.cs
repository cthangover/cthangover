using Cthangover.Core.Actions;

namespace Cthangover.Mods.Map
{
    /// <summary>
    /// Thin adapter from the scenario action layer to the process-wide
    /// <see cref="MapZoneService"/> singleton, following the same shape as
    /// <see cref="QuestServiceImpl"/>: the interface exists so
    /// <see cref="IActionContext"/> can be mocked in tests, while the actual
    /// state always lives in the singleton and therefore survives for the whole
    /// session and lands in the save through <c>RuntimeSerializer</c>.
    /// </summary>
    public class MapZoneServiceImpl : IMapZoneService
    {
        /// <summary>True when the zone is currently open for travel.</summary>
        public bool IsOpen(string id) => MapZoneService.Instance.IsOpen(id);

        /// <summary>True when the zone is currently visible on the map.</summary>
        public bool IsVisible(string id) => MapZoneService.Instance.IsVisible(id);

        /// <summary>Opens the zone for travel (overrides the map JSON's "locked" flag; persisted in the save).</summary>
        public void Open(string id) => MapZoneService.Instance.Open(id);

        /// <summary>Closes the zone — the marker stays but travel is blocked (persisted in the save).</summary>
        public void Close(string id) => MapZoneService.Instance.Close(id);

        /// <summary>Reveals a hidden zone on the map (persisted in the save).</summary>
        public void Show(string id) => MapZoneService.Instance.Show(id);

        /// <summary>Hides a zone from the map entirely (persisted in the save).</summary>
        public void Hide(string id) => MapZoneService.Instance.Hide(id);
    }
}
