using Cthangover.Core.Actions;

namespace Cthangover.Mods.Map
{

/// <summary>
///     Opens a map zone for travel by ID. The counterpart of
///     <see cref="MapCloseZoneAction"/>: authors close a zone in the map JSON
///     (<c>"locked": true</c>) and then open it from a scenario when the story
///     allows visiting it, e.g. after a quest tag is granted. The override is
///     stored by the map service and persisted in the save, so the zone stays
///     open across scene changes and play sessions.
/// </summary>
public class MapOpenZoneAction : IScenarioAction
{
    /// <summary>
    ///     Registered as "map.open_zone" — opens the zone named by the
    ///     required "zone_id" dialog variable. Unknown IDs are ignored with a
    ///     warning, so a scenario can safely reference a zone added by another
    ///     mod that may not be installed.
    /// </summary>
    public string Name => "map.open_zone";

    /// <summary>
    ///     Reads "zone_id" and delegates to the map service. Missing
    ///     "zone_id" is a script authoring error: it is logged and the action
    ///     does nothing instead of throwing inside dialog execution.
    /// </summary>
    public void Run(IActionContext ctx)
    {
        var zoneId = ctx.GetParam("zone_id");

        if (string.IsNullOrEmpty(zoneId))
        {
            ctx.Log("EVENT", "MapOpenZoneAction: missing 'zone_id' variable");
            return;
        }

        ctx.Map.Open(zoneId);
        ctx.Log("MAP", $"MapOpenZoneAction: open '{zoneId}'");
    }
}
}
