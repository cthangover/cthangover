namespace Cthangover.Core.Actions.Atomic
{
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
