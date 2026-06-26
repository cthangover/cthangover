using Cthangover.Core.Battle;

namespace Cthangover.Core.Actions.Atomic
{
    /// <summary>
    /// Selects which battle core (combat ruleset) the next battle will use.
    /// The "core" variable maps to a BattleCoreRegistry entry by string ID.
    /// Must be called before a battle starts, as BattleServiceImpl.Init reads
    /// the active core at initialization time.
    /// </summary>
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
