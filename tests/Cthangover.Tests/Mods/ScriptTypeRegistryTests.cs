using System.Collections.Generic;
using Cthangover.Core.Mods;
using Godot;

namespace Engine.Tests.Mods
{
    public class ScriptTypeRegistryTests
    {
        private class RegistryTestNode : Node
        {
        }

        [Fact]
        public void ResolveByPath_NormalizesScriptPaths()
        {
            ScriptTypeRegistry.Clear();
            try
            {
                ScriptTypeRegistry.RegisterAssembly(
                    typeof(RegistryTestNode).Assembly,
                    "test_mod",
                    new List<(string, string)> { ("res://scripts/RegistryTestNode.cs", "") });

                Assert.Same(typeof(RegistryTestNode), ScriptTypeRegistry.ResolveByPath("scripts/RegistryTestNode.cs"));
                Assert.Same(typeof(RegistryTestNode), ScriptTypeRegistry.ResolveByPath("scripts\\RegistryTestNode.cs"));
                Assert.Same(typeof(RegistryTestNode), ScriptTypeRegistry.ResolveByPath("res://scripts/RegistryTestNode.cs"));
                Assert.Same(typeof(RegistryTestNode), ScriptTypeRegistry.ResolveByPath("scripts/../scripts/RegistryTestNode.cs"));
            }
            finally
            {
                ScriptTypeRegistry.Clear();
            }
        }

        [Fact]
        public void RemoveMod_ForgetsThatModsTypes_KeepsOthers()
        {
            ScriptTypeRegistry.Clear();
            try
            {
                var sources = new List<(string, string)> { ("res://scripts/RegistryTestNode.cs", "") };
                ScriptTypeRegistry.RegisterAssembly(typeof(RegistryTestNode).Assembly, "test_mod", sources);
                ScriptTypeRegistry.RegisterAssembly(typeof(RegistryTestNode).Assembly, "other_mod", sources);

                ScriptTypeRegistry.RemoveMod("test_mod");
                Assert.Same(typeof(RegistryTestNode), ScriptTypeRegistry.ResolveByPath("scripts/RegistryTestNode.cs"));

                ScriptTypeRegistry.RemoveMod("other_mod");
                Assert.Null(ScriptTypeRegistry.ResolveByPath("scripts/RegistryTestNode.cs"));
                Assert.Null(ScriptTypeRegistry.ResolveByName(nameof(RegistryTestNode)));

                ScriptTypeRegistry.RemoveMod(null);
                ScriptTypeRegistry.RemoveMod("never_registered");
            }
            finally
            {
                ScriptTypeRegistry.Clear();
            }
        }
    }
}
