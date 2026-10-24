namespace Cthangover.Mods.Stats.Tests
{
    using Cthangover.Core.Mods.Testing;

    /// <summary>
    /// The facade's "who" rules and the opt-in of the battle bridge: a character with no
    /// stats data of any kind is left alone, one that gains a bonus takes part.
    /// </summary>
    public class StatsServiceTests
    {
        private const string Nobody = "test_nobody_has_stats";

        [ModTest("service/who_resolves")]
        static void WhoResolves(ModTestContext ctx)
        {
            ModAssert.Equal(Nobody, StatsService.Resolve(" " + Nobody + " "), "an id is taken as written, trimmed");
            ModAssert.NotNull(StatsService.Resolve("player"), "player always resolves to someone");
            ModAssert.Equal(StatsService.Resolve(null), StatsService.Resolve("PLAYER"), "player in any case is the default");
            ModAssert.True(StatsService.Targets("party").Count >= 1, "party is the roster, or the player alone");
            ModAssert.Equal(1, StatsService.Targets(Nobody).Count, "an id is one target");
        }

        [ModTest("service/participation_is_opt_in")]
        static void ParticipationIsOptIn(ModTestContext ctx)
        {
            var saved = StatState.Instance.Data;
            try
            {
                StatState.Instance.Data = new StatStateData();
                ModAssert.False(StatsService.Participates(Nobody), "no archetype, no stats data, no bonus, no gear: not touched");
                StatsService.AddBonus(Nobody, "str", 1);
                ModAssert.True(StatsService.Participates(Nobody), "a bonus gained during play opts in");
                ModAssert.CloseTo(1, StatsService.Sheet(Nobody).GetBase("str") - (StatsService.Definition("str")?.Default ?? 0), what: "and shows on the sheet");
                StatsService.SetArchetype(Nobody, "some_archetype");
                ModAssert.Equal("some_archetype", StatsService.ArchetypeOf(Nobody), "a chosen archetype wins over the template's");
            }
            finally
            {
                StatState.Instance.Data = saved;
                StatsService.ClearCache();
            }
        }
    }
}
