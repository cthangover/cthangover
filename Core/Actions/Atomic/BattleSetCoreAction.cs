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
        /// <summary>
        /// Registered as "battle.set_core" — selects the combat ruleset
        /// for the next battle encounter. The "core" dialog variable maps
        /// to a BattleCoreRegistry entry ID. Must be called before
        /// battle.init, as BattleServiceImpl.Init reads the active core at
        /// initialization time. If "core" is empty or missing, logs a
        /// warning and returns without modifying the registry.
        /// </summary>
        public string Name => "battle.set_core";

        /// <summary>
        /// Reads the "core" variable and sets it as the active battle core
        /// via BattleCoreRegistry.Instance.SetActive. The core determines
        /// combat mechanics (turn order, ability system, etc.) for the
        /// next initiated battle.
        /// </summary>
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
