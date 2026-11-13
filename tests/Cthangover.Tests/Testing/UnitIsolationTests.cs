namespace Cthangover.Tests.Testing
{
    using System;
    using System.IO;
    using Cthangover.Core.Mods;

    /// <summary>Non-Godot persistence must honor the runner's user root even when VSTest changes its working directory.</summary>
    public class UnitIsolationTests
    {
        [Fact]
        public void ModOrderUsesPrivateDataRatherThanTheTestAssemblyDirectory()
        {
            var data = Environment.GetEnvironmentVariable("CTHANGOVER_TEST_DATA");
            var expected = string.IsNullOrWhiteSpace(data) ? "mod_load_order.json" : Path.Combine(data, "mod_load_order.json");
            Assert.Equal(expected, ModLoadOrderStore.Path);
        }
    }
}
