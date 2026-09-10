using System.Collections.Generic;
using Cthangover.Core.Mods;
using Xunit;

namespace Engine.Tests.Mods
{
    public class ModLoadOrderTests
    {
        [Fact]
        public void Compute_Empty_ReturnsEmpty()
        {
            var mods = new Dictionary<string, IReadOnlyList<string>>();
            var result = ModLoadOrder.Compute(mods);
            Assert.Empty(result);
        }

        [Fact]
        public void Compute_SingleMod_ReturnsSingle()
        {
            var mods = new Dictionary<string, IReadOnlyList<string>>
            {
                ["core"] = null
            };
            var result = ModLoadOrder.Compute(mods);
            Assert.Equal(new[] { "core" }, result);
        }

        [Fact]
        public void Compute_NoDependencies_ReturnsAlphabetical()
        {
            var mods = new Dictionary<string, IReadOnlyList<string>>
            {
                ["zebra"] = null,
                ["alpha"] = null,
                ["mega"] = null
            };
            var result = ModLoadOrder.Compute(mods);
            Assert.Equal(new[] { "alpha", "mega", "zebra" }, result);
        }

        [Fact]
        public void Compute_SingleDependency_DependentAfterDependency()
        {
            var mods = new Dictionary<string, IReadOnlyList<string>>
            {
                ["core"] = null,
                ["addon"] = new List<string> { "core" }
            };
            var result = ModLoadOrder.Compute(mods);
            Assert.Equal("core", result[0]);
            Assert.Equal("addon", result[1]);
        }

        [Fact]
        public void Compute_ChainOfThree_DeepestLast()
        {
            var mods = new Dictionary<string, IReadOnlyList<string>>
            {
                ["base"] = null,
                ["middle"] = new List<string> { "base" },
                ["top"] = new List<string> { "middle" }
            };
            var result = ModLoadOrder.Compute(mods);
            Assert.Equal(new[] { "base", "middle", "top" }, result);
        }

        [Fact]
        public void Compute_MultipleDependents_SameLevelAlphabetical()
        {
            var mods = new Dictionary<string, IReadOnlyList<string>>
            {
                ["core"] = null,
                ["tools"] = null,
                ["interface"] = new List<string> { "core" },
                ["zeta_mod"] = new List<string> { "core", "interface" },
                ["alpha_mod"] = new List<string> { "core", "interface" },
                ["cooking"] = new List<string> { "core", "interface" }
            };
            var result = ModLoadOrder.Compute(mods);

            Assert.Equal("core", result[0]);
            Assert.Equal("tools", result[1]);
            Assert.Equal("interface", result[2]);
            Assert.Equal("alpha_mod", result[3]);
            Assert.Equal("cooking", result[4]);
            Assert.Equal("zeta_mod", result[5]);
        }

        [Fact]
        public void Compute_ManualOrderWithinSameLevel_Respected()
        {
            var mods = new Dictionary<string, IReadOnlyList<string>>
            {
                ["core"] = null,
                ["tools"] = null,
                ["cooking"] = new List<string> { "core" },
                ["hunting"] = new List<string> { "core" },
                ["fishing"] = new List<string> { "core" }
            };
            var manual = new List<string> { "fishing", "hunting", "cooking", "core", "tools" };

            var result = ModLoadOrder.Compute(mods, manual);

            Assert.Equal("core", result[0]);
            Assert.Equal("tools", result[1]);
            Assert.Equal("fishing", result[2]);
            Assert.Equal("hunting", result[3]);
            Assert.Equal("cooking", result[4]);
        }

        [Fact]
        public void Compute_ManualOrderCannotViolateDependencies()
        {
            var mods = new Dictionary<string, IReadOnlyList<string>>
            {
                ["core"] = null,
                ["addon"] = new List<string> { "core" }
            };
            var manual = new List<string> { "addon", "core" };

            var result = ModLoadOrder.Compute(mods, manual);

            Assert.Equal("core", result[0]);
            Assert.Equal("addon", result[1]);
        }

        [Fact]
        public void Compute_MissingDependency_IgnoredModLoadsNormally()
        {
            var mods = new Dictionary<string, IReadOnlyList<string>>
            {
                ["core"] = null,
                ["addon"] = new List<string> { "nonexistent" }
            };
            var result = ModLoadOrder.Compute(mods);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public void Compute_CircularDependency_DoesNotCrash()
        {
            var mods = new Dictionary<string, IReadOnlyList<string>>
            {
                ["a"] = new List<string> { "b" },
                ["b"] = new List<string> { "a" }
            };
            var cycles = new List<string>();
            ModLoadOrder.ComputeDependsLevels(mods, new List<string> { "a", "b" }, id => cycles.Add(id));

            Assert.NotEmpty(cycles);
        }

        [Fact]
        public void ComputeDependsLevels_LinearChain_CorrectDepths()
        {
            var mods = new Dictionary<string, IReadOnlyList<string>>
            {
                ["base"] = null,
                ["mid"] = new List<string> { "base" },
                ["top"] = new List<string> { "mid" }
            };
            var levels = ModLoadOrder.ComputeDependsLevels(mods, new List<string> { "base", "mid", "top" });

            Assert.Equal(0, levels["base"]);
            Assert.Equal(1, levels["mid"]);
            Assert.Equal(2, levels["top"]);
        }

        [Fact]
        public void ComputeDependsLevels_DiamondDependency_CorrectDepths()
        {
            var mods = new Dictionary<string, IReadOnlyList<string>>
            {
                ["base"] = null,
                ["left"] = new List<string> { "base" },
                ["right"] = new List<string> { "base" },
                ["top"] = new List<string> { "left", "right" }
            };
            var levels = ModLoadOrder.ComputeDependsLevels(mods,
                new List<string> { "base", "left", "right", "top" });

            Assert.Equal(0, levels["base"]);
            Assert.Equal(1, levels["left"]);
            Assert.Equal(1, levels["right"]);
            Assert.Equal(2, levels["top"]);
        }

        [Fact]
        public void Compute_RealWorldModSet_CorrectOrder()
        {
            var mods = new Dictionary<string, IReadOnlyList<string>>
            {
                ["core"] = null,
                ["tools"] = null,
                ["interface"] = new List<string> { "core" },
                ["card_battle"] = new List<string> { "core", "interface" },
                ["cooking"] = new List<string> { "core", "interface" },
                ["ff_battle"] = new List<string> { "core", "interface" },
                ["murakami"] = new List<string> { "core", "interface" },
                ["test_interactives"] = new List<string> { "core", "interface" }
            };

            var result = ModLoadOrder.Compute(mods);

            Assert.Equal(8, result.Count);
            Assert.Equal("core", result[0]);
            Assert.Equal("tools", result[1]);
            Assert.Equal("interface", result[2]);
            Assert.Equal("card_battle", result[3]);
            Assert.Equal("cooking", result[4]);
            Assert.Equal("ff_battle", result[5]);
            Assert.Equal("murakami", result[6]);
            Assert.Equal("test_interactives", result[7]);
        }

        [Fact]
        public void Compute_RealWorldModsWithManual_CardBattleBeforeCooking()
        {
            var mods = new Dictionary<string, IReadOnlyList<string>>
            {
                ["core"] = null,
                ["tools"] = null,
                ["interface"] = new List<string> { "core" },
                ["card_battle"] = new List<string> { "core", "interface" },
                ["cooking"] = new List<string> { "core", "interface" }
            };

            var manual = new List<string> { "cooking", "card_battle", "core", "tools", "interface" };

            var result = ModLoadOrder.Compute(mods, manual);

            Assert.Equal("core", result[0]);
            Assert.Equal("tools", result[1]);
            Assert.Equal("interface", result[2]);
            Assert.Equal("cooking", result[3]);
            Assert.Equal("card_battle", result[4]);
        }

        [Fact]
        public void Compute_ModsWithoutManifest_NoDepends_TreatedAsLevelZero()
        {
            var mods = new Dictionary<string, IReadOnlyList<string>>
            {
                ["core"] = null,
                ["test_mod"] = null,
                ["test_mod_a"] = null,
                ["test_mod_b"] = null,
                ["cooking"] = new List<string> { "core" }
            };

            var result = ModLoadOrder.Compute(mods);

            Assert.Equal("core", result[0]);
            Assert.Equal("test_mod", result[1]);
            Assert.Equal("test_mod_a", result[2]);
            Assert.Equal("test_mod_b", result[3]);
            Assert.Equal("cooking", result[4]);
        }

        [Fact]
        public void Compute_PartialManualOrder_NewModsAlphabeticalAfterManual()
        {
            var mods = new Dictionary<string, IReadOnlyList<string>>
            {
                ["core"] = null,
                ["tools"] = null,
                ["zeta_mod"] = new List<string> { "core" },
                ["alpha_mod"] = new List<string> { "core" },
                ["cooking"] = new List<string> { "core" }
            };
            var manual = new List<string> { "cooking" };

            var result = ModLoadOrder.Compute(mods, manual);

            Assert.Equal("core", result[0]);
            Assert.Equal("tools", result[1]);
            Assert.Equal("cooking", result[2]);
            Assert.Equal("alpha_mod", result[3]);
            Assert.Equal("zeta_mod", result[4]);
        }
    }
}
