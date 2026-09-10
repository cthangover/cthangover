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
    }
}
