using Cthangover.Core.Battle;

namespace Cthangover.Core.Actions.Atomic
{
    public class BattleSetCoreAction : IScenarioAction
    {
        public string Name => "battle.set_core";

        public void Run(IActionContext ctx)
        {
            var core = ctx.GetParam("core");
            if (string.IsNullOrEmpty(core))
            {
                ctx.Log("EVENT", "BattleSetCoreAction: missing 'core' variable");
                return;
            }

            BattleCoreRegistry.Instance.SetActive(core);
            ctx.Log("EVENT", $"BattleSetCoreAction: battle core set to '{core}'");
        }
    }
}
