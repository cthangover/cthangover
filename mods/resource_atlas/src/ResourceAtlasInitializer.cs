using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.ResourceAtlas
{
    /// <summary>
    /// Activates the atlas only on demand: the <c>--resource-atlas</c> user
    /// arg (after <c>--</c>) queues <see cref="ResourceAtlasDriverNode"/>,
    /// which waits for the boot, writes the report and quits. Optional
    /// <c>--atlas-out=&lt;dir&gt;</c> (res://, user:// or absolute) sets the
    /// report directory, <c>--timeout=&lt;s&gt;</c> the watchdog.
    /// </summary>
    public class ResourceAtlasInitializer : IModInitializer
    {
        public const string Flag = "--resource-atlas";
        private const string OutPrefix = "--atlas-out=";
        private const string DefaultOut = "user://resource_atlas";

        private static bool queued;

        public void OnModLoaded(string modId) { }

        public void OnModResourcesReady()
        {
            if (queued)
                return;

            var args = OS.GetCmdlineUserArgs();
            if (!args.Contains(Flag))
                return;

            var output = args.FirstOrDefault(a => a.StartsWith(OutPrefix, System.StringComparison.Ordinal))?.Substring(OutPrefix.Length);
            if (string.IsNullOrWhiteSpace(output))
                output = DefaultOut;

            queued = true;
            var directory = ProjectSettings.GlobalizePath(output);
            GameLogger.Log("RESOURCE_ATLAS", $"resource atlas requested, report directory '{directory}'");
            (Godot.Engine.GetMainLoop() as SceneTree)?.Root?.CallDeferred("add_child", new ResourceAtlasDriverNode(directory, ParseTimeout(args)));
        }

        private static float ParseTimeout(string[] args)
        {
            var raw = args.FirstOrDefault(a => a.StartsWith("--timeout=", System.StringComparison.Ordinal))?.Substring("--timeout=".Length);
            return float.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? seconds
                : 120f;
        }
    }
}
