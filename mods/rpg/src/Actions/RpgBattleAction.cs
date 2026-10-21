using Cthangover.Core.Actions;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// <c>action rpg.battle enemies=slime,slime [win_flag=slime_dead]</c> — a
    /// battle that returns to the map the player stands on. Unlike
    /// <c>battle.init</c> + <c>switch_scene Battle</c> it needs no return scene
    /// and can mark the victory with a story flag. End the scenario after it.
    /// </summary>
    public class RpgBattleAction : IScenarioAction
    {
        /// <summary>DSL name.</summary>
        public string Name => "rpg.battle";

        /// <summary>Reads <c>enemies</c> and <c>win_flag</c> and starts the battle.</summary>
        public void Run(IActionContext ctx)
        {
            var enemies = ctx.GetParam("enemies");
            if (string.IsNullOrWhiteSpace(enemies))
            {
                ctx.LogWarning("RPG", "rpg.battle: missing 'enemies'");
                return;
            }
            RpgBattleService.Start(enemies, ctx.GetParam("win_flag"));
        }
    }
}
