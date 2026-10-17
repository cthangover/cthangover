namespace Cthangover.Core.Mods.HotReload
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// When the mod hot reload watches files, and which file changes count.
    /// On by default only when the game runs from the Godot editor (the
    /// developer's play loop: edit a mod, see it a moment later); exported
    /// builds never watch. Automated runs (integration tests, atlases, mod
    /// unit tests) are excluded even from the editor, because they write mod
    /// files themselves and would reload under their own feet — a test that
    /// exercises hot reload opts in with <see cref="EnableFlag"/>.
    /// </summary>
    public static class HotReloadSettings
    {
        /// <summary>User arg forcing the watcher on (any build, any run).</summary>
        public const string EnableFlag = "--hot-reload";

        /// <summary>User arg forcing the watcher off.</summary>
        public const string DisableFlag = "--no-hot-reload";

        private static readonly string[] AutomatedRunPrefixes =
        {
            "--itest=", "--run-tests", "--timeout=", "--choices=", "--resource-atlas", "--locale-atlas",
        };

        private static readonly string[] IgnoredExtensions =
        {
            ".uid", ".import", ".tmp", ".swp", ".bak", "~",
        };

        private static readonly string[] IgnoredSegments =
        {
            "/.godot/", "/.git/", "/.vs/", "/.idea/", "/bin/", "/obj/",
        };

        /// <summary>Decides whether the watcher runs for this process.</summary>
        public static bool IsEnabled(bool runFromEditor, IEnumerable<string> userArgs)
        {
            var args = new List<string>(userArgs ?? Array.Empty<string>());
            if (args.Contains(DisableFlag))
                return false;
            if (args.Contains(EnableFlag))
                return true;
            if (!runFromEditor)
                return false;
            foreach (var arg in args)
            {
                foreach (var prefix in AutomatedRunPrefixes)
                {
                    if (arg.StartsWith(prefix, StringComparison.Ordinal))
                        return false;
                }
            }
            return true;
        }

        /// <summary>
        /// True for a change that can alter the mod set: sources, manifests,
        /// data, scenes, locale, assets. Editor bookkeeping (<c>.uid</c>,
        /// <c>.import</c>), editor swap files and build folders are ignored.
        /// </summary>
        public static bool IsRelevantChange(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;
            var normalized = path.Replace('\\', '/');
            foreach (var segment in IgnoredSegments)
            {
                if (normalized.IndexOf(segment, StringComparison.OrdinalIgnoreCase) >= 0)
                    return false;
            }
            var name = normalized.Substring(normalized.LastIndexOf('/') + 1);
            if (name.Length == 0 || name.StartsWith(".", StringComparison.Ordinal) || name.StartsWith("~", StringComparison.Ordinal))
                return false;
            foreach (var extension in IgnoredExtensions)
            {
                if (name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }
    }
}
