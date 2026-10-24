namespace Cthangover.Mods.BattleFormulas
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Battle;
    using Cthangover.Core.Characters;
    using Cthangover.Core.Events;
    using Cthangover.Mods.Stats;

    /// <summary>
    /// The pool stats (<c>"Pool": true</c>, like mana) of party members who take part in
    /// the stats, as bars of the battle cores: current = what is left after spending, max =
    /// the sheet value. Hidden stats and enemies (they never pay costs) get none.
    /// </summary>
    public class PoolGauge : IBattleUnitGauge
    {
        /// <inheritdoc />
        public int Order => 100;

        /// <inheritdoc />
        public IEnumerable<BattleGauge> Read(Character unit)
        {
            if (unit?.ID == null || StatsService.RosterEntry(unit.ID) == null || !StatsService.Participates(unit.ID))
                yield break;
            var sheet = StatsService.Sheet(unit.ID);
            foreach (var def in StatsService.Definitions.Where(d => d.Pool && !d.Hidden && sheet.Has(d.ID)))
            {
                yield return new BattleGauge
                {
                    Id = def.ID,
                    Label = string.IsNullOrWhiteSpace(def.Short) ? def.Name ?? def.ID : def.Short,
                    Current = (int)Math.Round(StatPools.Current(unit.ID, def.ID), MidpointRounding.AwayFromZero),
                    Max = sheet.Rounded(def.ID),
                };
            }
        }

        /// <summary>Stats changed (mana spent or refilled): the cores redraw that unit's bars.</summary>
        public static void OnStatsChanged(StatsChangedEvent evt) => BattleUnitGauges.NotifyChanged(evt?.CharacterId);
    }
}
