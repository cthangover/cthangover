using System.Collections.Generic;
using System.Text.Json;
using Cthangover.Core.Skills;
using Cthangover.Core.Utils;

namespace Engine.Tests.Skills
{
    /// <summary>
    /// Locks the contract of the collection effect plate: skills from any mod
    /// are summed by effect key in first-seen order, JSON-authored effects
    /// survive a full serialize/deserialize round-trip, and display formatting
    /// stays stable (explicit sign, percent suffix, trimmed zeros). The panel
    /// and the card both render through this logic, so a regression here would
    /// silently misreport what the whole collection grants.
    /// </summary>
    public class SkillEffectTests
    {
        [Fact]
        public void Aggregate_SumsSameKey_PreservingFirstSeenOrder()
        {
            var skills = new List<SkillInfo>
            {
                new()
                {
                    Effects = new List<SkillEffectInfo>
                    {
                        new() { Key = "characters/skills/effects/recruit", Value = 10f, Percent = true },
                        new() { Key = "characters/skills/effects/loot", Value = 5f, Percent = true },
                    },
                },
                new()
                {
                    Effects = new List<SkillEffectInfo>
                    {
                        new() { Key = "characters/skills/effects/recruit", Value = 40f, Percent = true },
                    },
                },
            };

            var totals = SkillEffectAggregator.Aggregate(skills);

            Assert.Equal(2, totals.Count);
            Assert.Equal("characters/skills/effects/recruit", totals[0].Key);
            Assert.Equal(50f, totals[0].Value);
            Assert.True(totals[0].Percent);
            Assert.Equal("characters/skills/effects/loot", totals[1].Key);
            Assert.Equal(5f, totals[1].Value);
        }

        [Fact]
        public void Aggregate_SkipsNullSkillsEffectsAndKeys()
        {
            var skills = new List<SkillInfo>
            {
                null,
                new() { Effects = null },
                new()
                {
                    Effects = new List<SkillEffectInfo>
                    {
                        null,
                        new() { Key = null, Value = 3f },
                        new() { Key = string.Empty, Value = 4f },
                        new() { Key = "recruit", Value = 5f },
                    },
                },
            };

            var total = Assert.Single(SkillEffectAggregator.Aggregate(skills));

            Assert.Equal("recruit", total.Key);
            Assert.Equal(5f, total.Value);
        }

        [Fact]
        public void Aggregate_NoSkills_ReturnsEmpty()
        {
            Assert.Empty(SkillEffectAggregator.Aggregate(new List<SkillInfo>()));
            Assert.Empty(SkillEffectAggregator.Aggregate(null));
        }

        [Fact]
        public void Aggregate_PercentFlagSurvivesMixedContributors()
        {
            var skills = new List<SkillInfo>
            {
                new() { Effects = new List<SkillEffectInfo> { new() { Key = "k", Value = 1f, Percent = false } } },
                new() { Effects = new List<SkillEffectInfo> { new() { Key = "k", Value = 2f, Percent = true } } },
            };

            Assert.True(Assert.Single(SkillEffectAggregator.Aggregate(skills)).Percent);
        }

        [Theory]
        [InlineData(10f, true, "+10%")]
        [InlineData(1f, false, "+1")]
        [InlineData(-5f, false, "-5")]
        [InlineData(-2.5f, true, "-2.5%")]
        [InlineData(0.5f, true, "+0.5%")]
        [InlineData(0f, true, "0%")]
        public void FormatValue_ProducesStableDisplay(float value, bool percent, string expected)
        {
            var effect = new SkillEffectInfo { Value = value, Percent = percent };

            Assert.Equal(expected, effect.FormatValue());
        }

        [Fact]
        public void SkillEffectInfo_JsonRoundTrip_KeepsAllFields()
        {
            var original = new SkillEffectInfo
            {
                Key = "characters/skills/effects/recruit",
                Value = 2.5f,
                Percent = true,
            };

            var json = JsonSerializer.Serialize(original, JsonOptionsProvider.WriteCompactOptions);
            var restored = JsonSerializer.Deserialize<SkillEffectInfo>(json, JsonOptionsProvider.ReadOptions);

            Assert.Equal(original.Key, restored.Key);
            Assert.Equal(original.Value, restored.Value);
            Assert.Equal(original.Percent, restored.Percent);
        }

        [Fact]
        public void SkillInfo_ParsesEffectsFromModJson()
        {
            const string json = """
            {
              "Id": "slave_master",
              "Name": "characters/skills/slave_master_name",
              "Type": 1,
              "Rare": 3,
              "Effects": [
                { "Key": "characters/skills/effects/recruit", "Value": 10, "Percent": true }
              ]
            }
            """;

            var skill = JsonSerializer.Deserialize<SkillInfo>(json, JsonOptionsProvider.ReadOptions);

            var effect = Assert.Single(skill.Effects);
            Assert.Equal("characters/skills/effects/recruit", effect.Key);
            Assert.Equal(10f, effect.Value);
            Assert.True(effect.Percent);
            Assert.Equal(SkillType.Passive, skill.SkillType);
            Assert.Equal(RareType.Epic, skill.RareType);
        }
    }
}
