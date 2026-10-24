namespace Cthangover.Tools.Schemas.Tests
{
    using System.Linq;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Utils;
    using Godot;

    /// <summary>Mounts <see cref="SchemaToolsTestDriver"/> when the game runs with <c>--itest=schema_tools_edit</c>.</summary>
    public class SchemaToolsTestInitializer : IModInitializer
    {
        private static bool driverQueued;

        /// <inheritdoc />
        public void OnModLoaded(string modId)
        {
        }

        /// <inheritdoc />
        public void OnModResourcesReady()
        {
            if (driverQueued || !OS.GetCmdlineUserArgs().Any(a => a == "--itest=schema_tools_edit"))
                return;
            driverQueued = true;
            GameLogger.Log("TEST", "SchemaToolsTestInitializer: activating schema_tools_edit");
            ModRoot.AddDeferred(new SchemaToolsTestDriver());
        }
    }
}
