using System;
using System.Collections.Generic;
using Godot;

namespace Cthangover.Core.Scenes
{
    /// <summary>
    /// Utility that recursively collects all <c>.tscn</c> files under
    /// <c>res://scenes</c> and returns them sorted by name. Used by
    /// tooling and scene selection UI.
    /// </summary>
    public static class ModScenes
    {
        /// <summary>
        /// Returns all .tscn files under res://scenes as (Name, Path)
        /// tuples, sorted alphabetically by name.
        /// </summary>
        public static List<(string Name, string Path)> CollectTscnFiles()
        {
            var result = new List<(string, string)>();
            Scan("res://scenes", result);
            result.Sort((a, b) => string.Compare(a.Item1, b.Item1, StringComparison.OrdinalIgnoreCase));
            return result;
        }

        private static void Scan(string dir, List<(string, string)> result)
        {
            using var d = DirAccess.Open(dir);
            if (d == null)
                return;

            d.ListDirBegin();
            var name = d.GetNext();
            while (name != "")
            {
                if (d.CurrentIsDir() && name != "." && name != "..")
                    Scan(dir.PathJoin(name), result);
                else if (name.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase))
                    result.Add((name, dir.PathJoin(name)));
                name = d.GetNext();
            }
            d.ListDirEnd();
        }
    }
}
