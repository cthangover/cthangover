using System;
using Cthangover.Core.Actions;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// <c>action rpg.teleport [map=forest] x=3 y=7 [dir=up]</c> — moves the player
    /// to a cell, on this map or another one (then the scene switches). The same
    /// effect a teleport event has, for scenarios: a cutscene that ends with the
    /// hero waking up at home, a ferryman who takes you across.
    /// </summary>
    public class RpgTeleportAction : IScenarioAction
    {
        /// <summary>DSL name.</summary>
        public string Name => "rpg.teleport";

        /// <summary>Parses the target and hands it to the world.</summary>
        public void Run(IActionContext ctx)
        {
            if (!int.TryParse(ctx.GetParam("x"), out var x) || !int.TryParse(ctx.GetParam("y"), out var y))
            {
                ctx.LogWarning("RPG", "rpg.teleport: 'x' and 'y' must be numbers");
                return;
            }

            var dir = Enum.TryParse<RpgDirection>(ctx.GetParam("dir"), true, out var parsed) ? parsed : RpgDirection.Down;
            RpgWorldHost.Teleport(new RpgTeleport { Map = ctx.GetParam("map"), X = x, Y = y, Dir = dir });
        }
    }
}
