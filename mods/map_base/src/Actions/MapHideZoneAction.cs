using Cthangover.Core.Actions;

namespace Cthangover.Mods.Map
{

/// <summary>
///     Hides a map zone by ID, removing it from the map until it is shown
///     again. Useful when a location is destroyed or temporarily inaccessible
///     in the story and the marker should disappear rather than appear closed.
///     The override is stored by the map service and persisted in the save.
/// </summary>
public class MapHideZoneAction : IScenarioAction
{
    /// <summary>
    ///     Registered as "map.hide_zone" — hides the zone named by the
    ///     required "zone_id" dialog variable. Unknown IDs are ignored with a
    ///     warning.
    /// </summary>
    public string Name => "map.hide_zone";

    /// <summary>
    ///     Reads "zone_id" and delegates to the map service. Missing
    ///     "zone_id" is logged and the action does nothing instead of throwing
    ///     inside dialog execution.
    /// </summary>
    public void Run(IActionContext ctx)
    {
        var zoneId = ctx.GetParam("zone_id");

        if (string.IsNullOrEmpty(zoneId))
        {
            ctx.Log("EVENT", "MapHideZoneAction: missing 'zone_id' variable");
            return;
        }

        ctx.Map.Hide(zoneId);
        ctx.Log("MAP", $"MapHideZoneAction: hide '{zoneId}'");
    }
}
}
