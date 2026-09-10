namespace Cthangover.Core.Actions.Atomic
{
    /// <summary>
    /// Dispatched by the scenario DSL to initiate battle. Reads "scene",
    /// "enemies", "quest_id" and "new_tag" from dialog variables. The enemies
    /// string is a comma-separated list embedded in the scenario script, which
    /// BattleServiceImpl.Init splits. Uses ctx.Battle.Init which constructs
    /// BattleData with the current background state — this means battle
    /// initialization must happen *after* the background has been set by a
    /// prior ActionBackground, otherwise the battle gets a null backdrop.
    /// </summary>
    public class BattleInitAction : IScenarioAction
    {
        /// <summary>
        /// Registered as "battle.init" — the scenario DSL's primary battle
        /// initiation command. Reads dialog variables "scene", "enemies",
        /// "quest_id", and "new_tag" via ctx.GetParam, then delegates to
        /// ctx.Battle.Init which constructs BattleData with the current
        /// background and lighting snapshots. Requires both "scene" and
        /// "enemies" to be non-empty — returns early with a log warning if
        /// either is missing. The enemies string is a comma-separated list
        /// embedded in the scenario script.
        /// </summary>
        public string Name => "battle.init";

        /// <summary>
        /// Reads battle parameters from dialog variables and initiates
        /// battle construction. Must be called after a background has been
        /// set (by a prior ActionBackground in the scenario) — otherwise
        /// the battle gets a null backdrop. Logs both success and failure
        /// cases for scenario debugging.
        /// </summary>
        public void Run(IActionContext ctx)
        {
            var sceneRaw = ctx.GetParam("scene");
            var enemies = ctx.GetParam("enemies");
            var questId = ctx.GetParam("quest_id");
            var newTag = ctx.GetParam("new_tag");

            if (string.IsNullOrEmpty(sceneRaw) || string.IsNullOrEmpty(enemies))
            {
                ctx.Log("EVENT", "BattleInitAction: missing 'scene' or 'enemies' variable");
                return;
            }

            ctx.Battle.Init(sceneRaw, enemies, questId, newTag);
            ctx.Log("EVENT", $"BattleInitAction: battle started at '{sceneRaw}' with enemies '{enemies}'");
        }
    }
}
