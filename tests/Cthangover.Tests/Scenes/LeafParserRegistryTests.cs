using System;
using Cthangover.Core.Mods.Unload;
using Cthangover.Core.Scenes;
using Cthangover.Core.Scenes.Parsing;

namespace Engine.Tests.Scenes
{
    /// <summary>
    /// Condition parsers leave with their mod: by the assembly their type lives in, or by the
    /// owner mod id they were registered with (GDScript conditions use a kernel type).
    /// </summary>
    public class LeafParserRegistryTests
    {
        private static bool Parses(string text)
        {
            try
            {
                return ConditionParser.Parse(text) != null;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        [Fact]
        public void Unregister_RemovesThatParserOnly()
        {
            var probe = new ProbeLeafParser("zz_probe_unregister");
            LeafParserRegistry.RegisterFirst(probe);
            Assert.True(Parses("zz_probe_unregister()"));

            Assert.True(LeafParserRegistry.Unregister(probe));
            Assert.False(Parses("zz_probe_unregister()"));
            Assert.False(LeafParserRegistry.Unregister(probe));
            Assert.True(Parses("door == \"open\""));
        }

        [Fact]
        public void RemoveOwned_ByOwnerModId()
        {
            var probe = new ProbeLeafParser("zz_probe_owner");
            LeafParserRegistry.RegisterFirst(probe, "probe_mod");
            Assert.True(Parses("zz_probe_owner()"));

            Assert.Equal(0, LeafParserRegistry.RemoveOwned(null, new[] { "other_mod" }));
            Assert.True(Parses("zz_probe_owner()"));
            Assert.Equal(1, LeafParserRegistry.RemoveOwned(null, new[] { "probe_mod" }));
            Assert.False(Parses("zz_probe_owner()"));
        }

        [Fact]
        public void RemoveOwned_ByAssemblyOfTheParserType()
        {
            var probe = new ProbeLeafParser("zz_probe_assembly");
            LeafParserRegistry.Register(probe);
            Assert.True(Parses("zz_probe_assembly()"));

            var removed = LeafParserRegistry.RemoveOwned(new AssemblyOwnership(new[] { typeof(ProbeLeafParser).Assembly }), Array.Empty<string>());
            Assert.True(removed >= 1);
            Assert.False(Parses("zz_probe_assembly()"));
            Assert.True(Parses("true && !false"), "kernel parsers stay");
        }
    }
}
