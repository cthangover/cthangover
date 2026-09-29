namespace Cthangover.Core.Actions;

/// <summary>
///     Scenario-facing facade over the map subsystem. Exposes only the four
///     state transitions authors need from scripts — open/close (travel access)
///     and show/hide (map visibility) — instead of the whole
///     <see cref="Cthangover.Core.UI.Tool.MapZoneService"/> surface. Kept as an
///     interface so <see cref="IActionContext"/> implementations used by tests
///     can substitute a lightweight mock, exactly like the quest and character
///     services.
/// </summary>
public interface IMapZoneService
{
    /// <summary>Effective open state of the zone; unknown IDs are closed.</summary>
    bool IsOpen(string id);

    /// <summary>Effective visibility of the zone; unknown IDs are invisible.</summary>
    bool IsVisible(string id);

    /// <summary>Opens the zone for travel. Unknown IDs are ignored with a warning.</summary>
    void Open(string id);

    /// <summary>Closes the zone for travel. Unknown IDs are ignored with a warning.</summary>
    void Close(string id);

    /// <summary>Reveals a hidden zone on the map. Unknown IDs are ignored with a warning.</summary>
    void Show(string id);

    /// <summary>Removes a zone from the map. Unknown IDs are ignored with a warning.</summary>
    void Hide(string id);
}
