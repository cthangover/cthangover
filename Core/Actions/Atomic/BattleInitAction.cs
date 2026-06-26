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
        public string Name => "battle.init";

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
