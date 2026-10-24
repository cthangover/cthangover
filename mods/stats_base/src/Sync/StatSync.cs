namespace Cthangover.Mods.Stats
{
    using System.Linq;
    using Cthangover.Core.Events;

    /// <summary>
    /// Keeps the party roster in step with the stats: applies level-ups from exp, teaches
    /// the archetype's actions (<see cref="StatLearnings"/>) and writes bound stats into
    /// each member's battle attributes. It runs whenever stats change and
    /// at the moments the roster may have changed behind the stats' back: entering a scene
    /// (back from a battle that awarded exp, after loading a save) and the end of a dialog
    /// (a scenario ran <c>party.add</c>). The battle bridge also runs it as a battle starts.
    /// </summary>
    public static class StatSync
    {
        private static bool syncing;

        /// <summary>Applies level-ups and bound stats to every roster member.</summary>
        public static void SyncRoster()
        {
            var roster = StatsService.Roster;
            if (roster == null || syncing)
                return;
            syncing = true;
            try
            {
                foreach (var pair in roster.Characters.ToList())
                {
                    if (StatProgression.ApplyLevelUps(pair.Key) > 0)
                        StatsService.Forget(pair.Key);
                    StatLearnings.Apply(pair.Key);
                    if (StatsService.Participates(pair.Key))
                        StatBindings.Apply(StatsService.Sheet(pair.Key), pair.Value?.Attributes);
                }
            }
            finally
            {
                syncing = false;
            }
        }

        /// <summary>Scene entered: the roster may hold exp from a battle or come from a save.</summary>
        public static void OnSceneEntered(SceneEnteredEvent evt) => StatsService.Changed(null);

        /// <summary>A dialog ended: a scenario may have added a party member or changed a level.</summary>
        public static void OnDialogEnded(DialogEndedEvent evt) => SyncRoster();
    }
}
