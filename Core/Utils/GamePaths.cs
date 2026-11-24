using System;
using System.IO;
using Godot;

namespace Cthangover.Core.Utils
{
    /// <summary>
    /// Absolute paths of the loose game trees (<c>mods/</c>, <c>collections/</c>,
    /// <c>config/</c>) that live next to the project in the editor and next to
    /// the executable in an exported build, plus classification of the virtual paths the game resolves
    /// itself.
    /// <para/>
    /// In an export <c>res://</c> is the pck, so
    /// <c>ProjectSettings.GlobalizePath("res://mods/")</c> collapses to a bare
    /// <c>mods/</c> relative to the working directory: the game then finds its
    /// content only when started from its own folder, and the Godot source
    /// generators get no project dir to derive mod <c>[ScriptPath]</c>s from.
    /// The release packaging puts those trees beside the executable, so that
    /// directory is the root here. Godot API — call on the main thread.
    /// <para/>
    /// Callers that accept a configurable path use <see cref="TryGetScheme"/>,
    /// <see cref="IsVirtualFile"/> or <see cref="NormalizeVirtual"/> instead of comparing schemes by hand:
    /// the debug host's discovery pointer is the first such caller, and keeping the scheme list here means a
    /// future scheme (for example a shared data tree) reaches every owner through one edit.
    /// </summary>
    public static class GamePaths
    {
        /// <summary>Virtual schemes the game resolves itself: the user profile tree and the game tree.</summary>
        public enum VirtualScheme
        {
            /// <summary><c>user://</c> — per-user writable profile data (logs, saves, pointers).</summary>
            User,

            /// <summary><c>res://</c> — the game tree: writable in the editor, packed read-only in exports.</summary>
            Res
        }

        /// <summary>
        /// Classifies a virtual path. Callers that only need "is this ours" use this instead of comparing
        /// schemes by hand, so adding a scheme later changes one place.
        /// </summary>
        public static bool TryGetScheme(string path, out VirtualScheme scheme)
        {
            scheme = VirtualScheme.User;
            if (path != null && path.StartsWith("user://", StringComparison.Ordinal))
            {
                return true;
            }

            if (path != null && path.StartsWith("res://", StringComparison.Ordinal))
            {
                scheme = VirtualScheme.Res;
                return true;
            }

            return false;
        }

        /// <summary>
        /// True when the value is a usable virtual file path of the requested schemes: a known scheme,
        /// no parent traversal and no backslashes. The engine resolves the rest.
        /// </summary>
        public static bool IsVirtualFile(string path, params VirtualScheme[] schemes)
        {
            if (string.IsNullOrWhiteSpace(path) || path.EndsWith('/') || path.Contains("..") || path.Contains('\\')
                || !TryGetScheme(path, out var scheme))
            {
                return false;
            }

            foreach (var allowed in schemes)
            {
                if (scheme == allowed)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns the trimmed value when it is a virtual file path of the allowed schemes, otherwise the
        /// fallback; the caller decides whether to warn about the rejected original.
        /// </summary>
        public static string NormalizeVirtual(string path, string fallback, params VirtualScheme[] schemes)
        {
            var value = (path ?? "").Trim();
            return IsVirtualFile(value, schemes) ? value : fallback;
        }

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

            if (root == null)
                throw new NullReferenceException("Can't read project path '" + OS.GetExecutablePath() + "'");

            return string.IsNullOrEmpty(relative)
                ? root.TrimEnd('/', '\\') + "/"
                : Path.Combine(root, relative).Replace('\\', '/');
        }
    }
}
