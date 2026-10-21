using System;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Resolvers;
using Cthangover.Core.Scenarios;

namespace Cthangover.Mods.Rpg
{
    /// <summary>
    /// Reads the scenario a map event points to. Event scenarios are not scene
    /// scenarios: they have no header (or one that is ignored) and play only when
    /// the event fires. The path is relative to the <c>scenarios</c> file group
    /// with an optional extension, and the usual file override applies (a
    /// dependent mod's file of the same path wins), so the map does not need to
    /// know which mod ships the text.
    /// </summary>
    public static class RpgScenarioSource
    {
        private const string Group = "scenarios";
        private const string Extension = ".scenario";

        /// <summary>Scenario body without its header, or null when no mod has the file.</summary>
        public static string ReadBody(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var wanted = path.Replace('\\', '/').Trim('/');
            if (wanted.StartsWith(Group + "/", StringComparison.OrdinalIgnoreCase))
                wanted = wanted.Substring(Group.Length + 1);
            if (!wanted.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
                wanted += Extension;

            foreach (var pair in Files.Collect(Group))
            {
                if (!string.Equals(pair.Key, wanted, StringComparison.OrdinalIgnoreCase))
                    continue;
                var text = ModManager.Instance.ReadFileText(pair.Value.ModId, pair.Value.FullPath);
                return text == null ? null : ScenarioMetadata.ParseMetadata(text).remainingText;
            }
            return null;
        }
    }
}
