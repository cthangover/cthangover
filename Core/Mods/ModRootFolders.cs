namespace Cthangover.Core.Mods
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Names of the game-directory folders that hold mods. The game dir may keep
    /// its mods in several sibling folders (public mods in <c>mods/</c>, mods of a
    /// private repository in <c>mods_private/</c>); every one of them is an equal
    /// scanner root of <see cref="Discovery.GameDirModScanner"/> with the same
    /// priority, so a mod behaves identically whichever folder it lies in.
    /// <para/>
    /// This list is the only place in the core that knows the folder names:
    /// anything that has to recognise a <c>&lt;folder&gt;/&lt;mod&gt;/...</c> project
    /// path (the prefix the Godot editor writes into cross-mod <c>ext_resource</c>
    /// links, the source path of a compiled mod script) asks here instead of
    /// comparing with the literal <c>"mods"</c>. The test runner mirrors the list
    /// in <c>Cthangover.TestRunner.ModFolders</c> and <c>Cthangover.csproj</c>
    /// excludes each folder from the core compilation; a unit test keeps the three
    /// in step. Adding a folder is one entry here plus those two mirrors and a
    /// <c>.gdignore</c> inside the new folder.
    /// <para/>
    /// Publishing tools (<c>sync_mods.py</c>, <c>build_mod_releases.py</c>, the
    /// site catalog, <c>build_release.sh</c>) deliberately read only
    /// <see cref="Primary"/>: a mod outside it can never be published by accident.
    /// </summary>
    public static class ModRootFolders
    {
        /// <summary>The public mods folder; always a scanner root, even before it exists.</summary>
        public const string Primary = "mods";

        /// <summary>Every mods folder of the game dir, <see cref="Primary"/> first.</summary>
        public static IReadOnlyList<string> Names { get; } = new[] { Primary, "mods_private" };

        /// <summary>True when <paramref name="folder"/> is one of <see cref="Names"/> (case-insensitive, as project paths are).</summary>
        public static bool Contains(string folder)
        {
            foreach (var name in Names)
            {
                if (name.Equals(folder, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Turns a project-relative path <c>&lt;folder&gt;/&lt;mod&gt;/rest</c> into the
        /// mod-relative <c>rest</c> when <c>&lt;folder&gt;</c> is a mods folder; any other
        /// path is returned unchanged. Expects forward slashes and no <c>res://</c>
        /// scheme. Resources are looked up by their path inside a mod, across all
        /// loaded mods, so the folder and the mod id carry no information.
        /// </summary>
        public static string StripModPrefix(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            var segments = path.Split('/');
            if (segments.Length >= 3 && Contains(segments[0]))
                return string.Join("/", segments, 2, segments.Length - 2);

            return path;
        }
    }
}
