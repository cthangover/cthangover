using Cthangover.Core.UI.Tool;

namespace Cthangover.Core.Actions
{
    /// <summary>
    /// Thin adapter from the scenario action layer to the process-wide
    /// <see cref="MapZoneService"/> singleton, following the same shape as
    /// <see cref="QuestServiceImpl"/>: the interface exists so
    /// <see cref="IActionContext"/> can be mocked in tests, while the actual
    /// state always lives in the singleton and therefore survives for the whole
    /// session and lands in the save through <c>RuntimeSerializer</c>.
    /// </summary>
    internal class MapZoneServiceImpl : IMapZoneService
    {
        public bool IsOpen(string id) => MapZoneService.Instance.IsOpen(id);

        public bool IsVisible(string id) => MapZoneService.Instance.IsVisible(id);

        public void Open(string id) => MapZoneService.Instance.Open(id);

        public void Close(string id) => MapZoneService.Instance.Close(id);

        public void Show(string id) => MapZoneService.Instance.Show(id);

        public void Hide(string id) => MapZoneService.Instance.Hide(id);
    }
}
