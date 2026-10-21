namespace Cthangover.Mods.Map.Tests
{
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Testing;

    /// <summary>
    /// Zone lists arrive in mod load order, and a mod that ships a zone with an
    /// id already defined replaces it — the same rule as every other mod
    /// resource, so a dependent mod can reshape a zone of the mod it builds on.
    /// </summary>
    public class MapZoneServiceTests
    {
        [ModTest("zones/later_definition_replaces_earlier")]
        static void LaterDefinitionReplacesEarlier(ModTestContext ctx)
        {
            var service = MapZoneService.Instance;
            try
            {
                service.LoadZones(new List<MapZone>
                {
                    new() { ID = "test_zone", Title = "owner" },
                    new() { ID = "test_other", Title = "other" },
                    new() { ID = "TEST_ZONE", Title = "dependent" },
                });

                ModAssert.Equal(2, service.Zones.Count, "zone count");
                ModAssert.Equal("dependent", service.Get("test_zone").Title, "the later definition wins");
                ModAssert.Equal("dependent", service.Zones[0].Title, "it keeps the replaced zone's place");
            }
            finally
            {
                service.ReloadFromMods();
            }
        }
    }
}
