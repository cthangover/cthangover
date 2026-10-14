using Cthangover.Core.Actions;

namespace Cthangover.Mods.Map
{

/// <summary>
///     Reveals a hidden map zone by ID. Hidden zones are completely absent from
///     the map — no polygon, no marker, no label, no zone counter entry — which
///     lets authors keep late-game locations out of the early map without
///     shipping separate map files. The override is stored by the map service
///     and persisted in the save.
/// </summary>
public class MapShowZoneAction : IScenarioAction
{
    /// <summary>
    ///     Registered as "map.show_zone" — reveals the zone named by the
    ///     required "zone_id" dialog variable. Unknown IDs are ignored with a
    ///     warning.
    /// </summary>
    public string Name => "map.show_zone";

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
            ctx.Log("EVENT", "MapShowZoneAction: missing 'zone_id' variable");
            return;
        }

        ctx.Map.Show(zoneId);
        ctx.Log("MAP", $"MapShowZoneAction: show '{zoneId}'");
    }
}
}
