using Cthangover.Core.Actions;

namespace Cthangover.Mods.Map
{

/// <summary>
///     Closes a map zone for travel by ID. Used for zones that were open by
///     default but must become unavailable at some story point, or to take back
///     access granted earlier. The override is stored by the map service and
///     persisted in the save, so the closure survives scene changes and play
///     sessions.
/// </summary>
public class MapCloseZoneAction : IScenarioAction
{
    /// <summary>
    ///     Registered as "map.close_zone" — closes the zone named by the
    ///     required "zone_id" dialog variable. Unknown IDs are ignored with a
    ///     warning.
    /// </summary>
    public string Name => "map.close_zone";

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
            ctx.Log("EVENT", "MapCloseZoneAction: missing 'zone_id' variable");
            return;
        }

        ctx.Map.Close(zoneId);
        ctx.Log("MAP", $"MapCloseZoneAction: close '{zoneId}'");
    }
}
}
