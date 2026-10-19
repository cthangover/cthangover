using System.IO;
using Godot;

namespace Cthangover.Core.Utils
{
    /// <summary>
    /// Absolute paths of the loose game trees (<c>mods/</c>, <c>collections/</c>,
    /// <c>config/</c>) that live next to the project in the editor and next to
    /// the executable in an exported build.
    /// <para/>
    /// In an export <c>res://</c> is the pck, so
    /// <c>ProjectSettings.GlobalizePath("res://mods/")</c> collapses to a bare
    /// <c>mods/</c> relative to the working directory: the game then finds its
    /// content only when started from its own folder, and the Godot source
    /// generators get no project dir to derive mod <c>[ScriptPath]</c>s from.
    /// The release packaging puts those trees beside the executable, so that
    /// directory is the root here. Godot API — call on the main thread.
    /// </summary>
    public static class GamePaths
    {
        /// <summary>
        /// Absolute path for a <c>res://</c> path of a loose game tree, with a
        /// trailing separator kept as given; any other path (<c>user://</c>)
        /// goes through <c>ProjectSettings.GlobalizePath</c> unchanged.
        /// </summary>
        public static string Globalize(string resPath)
        {
            if (OS.HasFeature("editor") || !resPath.StartsWith("res://"))
                return ProjectSettings.GlobalizePath(resPath);

            var relative = resPath.Substring("res://".Length);
            var root = Path.GetDirectoryName(OS.GetExecutablePath());
            return string.IsNullOrEmpty(relative)
                ? root.TrimEnd('/', '\\') + "/"
                : Path.Combine(root, relative).Replace('\\', '/');
        }
    }
}
