namespace Cthangover.Mods.Stats.Tests
{
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Mods.Testing;

    /// <summary>
    /// Stats keys arrive as JSON extension data of foreign entities; they must read the way
    /// authors write them: any key case, numbers or formula texts, broken parts skipped.
    /// </summary>
    public class ExtraDataTests
    {
        private static Dictionary<string, object> Extra(string json)
        {
            var result = new Dictionary<string, object>();
            using var doc = JsonDocument.Parse(json);
            foreach (var prop in doc.RootElement.EnumerateObject())
                result[prop.Name] = prop.Value.Clone();
            return result;
        }

        [ModTest("extra/stats_numbers_and_formulas")]
        static void StatsNumbersAndFormulas(ModTestContext ctx)
        {
            var stats = ExtraData.Stats(Extra("{\"stats\": {\"STR\": 8, \"max_hp\": \"20 + vit * 4\", \"bad\": [1]}}"), "test");

            ModAssert.Equal(2, stats.Count, "the array entry is skipped");
            ModAssert.Equal("8", stats["str"], "number, key lowercased");
            ModAssert.Equal("20 + vit * 4", stats["MAX_HP"], "formula text, lookup ignores case");
        }

        [ModTest("extra/traits_and_text")]
        static void TraitsAndText(ModTestContext ctx)
        {
            var extra = Extra("{\"Archetype\": \"mage\", \"traits\": [{\"stat\": \"mag\", \"add\": 3}, {\"Stat\": \"mp\", \"Percent\": 10, \"Formula\": \"level\"}]}");

            var traits = ExtraData.Traits(extra, "test");

            ModAssert.Equal("mage", ExtraData.Text(extra, "archetype"), "text in any case");
            ModAssert.Equal(2, traits.Count, "two traits");
            ModAssert.Equal("mag", traits[0].Stat, "first stat");
            ModAssert.CloseTo(3, traits[0].Add, what: "first add");
            ModAssert.CloseTo(10, traits[1].Percent, what: "second percent");
            ModAssert.Equal("level", traits[1].Formula, "second formula");
        }

        [ModTest("extra/missing_or_malformed_reads_empty")]
        static void MissingOrMalformed(ModTestContext ctx)
        {
            ModAssert.Equal(0, ExtraData.Traits(null, "test").Count, "null extension data");
            ModAssert.Equal(0, ExtraData.Traits(Extra("{\"Traits\": 5}"), "test").Count, "a number is not a list");
            ModAssert.Equal(0, ExtraData.Stats(Extra("{\"Stats\": \"x\"}"), "test").Count, "a string is not an object");
            ModAssert.Null(ExtraData.Text(Extra("{\"Archetype\": {}}"), "Archetype"), "an object is not a text");
        }
    }
}
