namespace Cthangover.Mods.BattleFormulas
{
    using System.Collections.Generic;
    using System.Linq;
    using Cthangover.Core.Battle.Actions;
    using Cthangover.Core.Characters;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Finds the action entities that carry a <c>Formula=</c> value and registers a
    /// <see cref="FormulaActionExecutor"/> for each as a global battle executor. Runs when
    /// mod resources are ready and again as every battle starts, so an action added in an
    /// editor or by a freshly loaded mod is playable in the next battle.
    /// <para/>
    /// The hub keys executors by action id and replaces on re-registration, so a reload
    /// simply re-points the ids at this assembly's executors.
    /// </summary>
    public static class FormulaActions
    {
        private const string FormulaPrefix = "Formula=";

        private static object snapshot;

        /// <summary>The ids of the loaded formula actions.</summary>
        public static List<string> Ids()
            => Entities.Collect<ActionCharacterInfo>().Values
                .Where(IsFormulaAction).Select(a => a.ID).OrderBy(id => id, System.StringComparer.Ordinal).ToList();

        /// <summary>Whether an action entity is driven by a formula.</summary>
        public static bool IsFormulaAction(ActionCharacterInfo info)
            => info?.Values != null && info.Values.Any(v => v != null && v.StartsWith(FormulaPrefix, System.StringComparison.OrdinalIgnoreCase));

        /// <summary>Registers an executor per formula action (only when the data snapshot changed).</summary>
        public static void Refresh()
        {
            var current = Entities.Collect<ActionCharacterInfo>();
            if (ReferenceEquals(current, snapshot))
                return;
            snapshot = current;
            var ids = Ids();
            foreach (var id in ids)
                ActionExecutorHub.Instance.RegisterGlobal(new FormulaActionExecutor(id));
            GameLogger.Log("BATTLE", $"battle formulas: {ids.Count} formula action(s) registered");
        }
    }
}
