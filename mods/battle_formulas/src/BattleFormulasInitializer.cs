namespace Cthangover.Mods.BattleFormulas
{
    using Cthangover.Core.Events;
    using Cthangover.Core.Mods;

    /// <summary>Registers the formula actions when resources are ready and hooks the battle start.</summary>
    public class BattleFormulasInitializer : IModInitializer
    {
        private static bool subscribed;

        /// <inheritdoc />
        public void OnModLoaded(string modId)
        {
        }

        /// <inheritdoc />
        public void OnModResourcesReady()
        {
            FormulaActions.Refresh();
            if (subscribed)
                return;
            subscribed = true;
            ModEventBus.Subscribe<SceneRouteActivatingEvent>(BattleStatsBridge.OnRouteActivating);
            ModEventBus.Subscribe<BattleStartedEvent>(BattleStatsBridge.OnBattleStarted);
            ModEventBus.Subscribe<StatsChangedEvent>(PoolGauge.OnStatsChanged);
        }
    }
}
