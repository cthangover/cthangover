using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.LocaleAtlas
{
    /// <summary>
    /// Activates the key map only on demand: the <c>--locale-atlas</c> user
    /// arg (after <c>--</c>) queues <see cref="LocaleAtlasDriverNode"/>,
    /// which waits for the boot, writes the report and quits. Optional
    /// <c>--locale-out=&lt;dir&gt;</c> (res://, user:// or absolute) sets the
    /// report directory, <c>--timeout=&lt;s&gt;</c> the watchdog.
    /// </summary>
    public class LocaleAtlasInitializer : IModInitializer
    {
        public const string Flag = "--locale-atlas";
        private const string OutPrefix = "--locale-out=";
        private const string DefaultOut = "user://locale_atlas";

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
            GameLogger.Log("LOCALE_ATLAS", $"locale atlas requested, report directory '{directory}'");
            ModRoot.AddDeferred(new LocaleAtlasDriverNode(directory, ParseTimeout(args)));
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
