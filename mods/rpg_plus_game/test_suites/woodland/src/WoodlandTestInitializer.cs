namespace Cthangover.Mods.RpgPlus.Tests
{
    using System.Linq;
    using Cthangover.Core.Mods;
    using Godot;

    /// <summary>Inert outside its opt-in suite; the story mod stays entirely data and scenarios.</summary>
    public class WoodlandTestInitializer : IModInitializer
    {
        private static bool queued;

        /// <inheritdoc />
        public void OnModLoaded(string id) { }

        /// <inheritdoc />
        public void OnModResourcesReady()
        {
            var args = OS.GetCmdlineUserArgs();
            if (queued || !args.Contains("--itest=woodland"))
                return;
            queued = true;
            string shots = null;
            foreach (var arg in args)
                if (arg.StartsWith("--shots="))
                    shots = arg.Substring(8);
            ModRoot.AddDeferred(new WoodlandTestDriver(args.Contains("--ward-ending=pact"), shots));
        }
    }
}
