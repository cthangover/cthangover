namespace Cthangover.Mods.BattleFormulas
{
    using System.Linq;
    using Cthangover.Core.Battle;
    using Cthangover.Core.Events;
    using Cthangover.Mods.Stats;

    /// <summary>
    /// The moment a battle starts: formula actions are (re)registered, the party's bound
    /// stats are synced (a member may have joined in the same dialog that started the
    /// fight), and enemies whose templates carry stats data (<c>Stats</c>,
    /// <c>Archetype</c> or <c>Traits</c>) get their battle attributes from their sheets. An
    /// enemy without stats data keeps its template's numbers, so old content fights as before.
    /// </summary>
    public static class BattleStatsBridge
    {
        /// <summary>
        /// The battle route is about to load: the battle scene builds its sides and HUD from
        /// the attributes right after this, so they get their stats now.
        /// </summary>
        public static void OnRouteActivating(SceneRouteActivatingEvent evt)
        {
            if (evt?.LogicalName == "Battle")
                OnBattleStarted(null);
        }

        /// <summary>
        /// Handler of <see cref="BattleStartedEvent"/> (also run on the route activation);
        /// every step is idempotent, so running twice changes nothing.
        /// </summary>
        public static void OnBattleStarted(BattleStartedEvent evt)
        {
            FormulaActions.Refresh();
            StatSync.SyncRoster();
            foreach (var enemy in BattleRequest.Current?.EnemiesCards?.Where(e => e != null) ?? Enumerable.Empty<Core.Characters.Character>())
            {
                if (StatsService.Participates(enemy.ID))
                    StatBindings.Apply(StatsService.Sheet(enemy.ID), enemy.Attributes);
            }
        }
    }
}
