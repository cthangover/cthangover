using System;
using System.Collections.Generic;
using System.IO;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Abstraction over mod storage backends. Every mod — whether a loose
    /// folder on disk or a compressed <c>.zip</c> archive — exposes its
    /// files through this single interface, so the entire asset pipeline
    /// (<c>ModManager</c>, factories, compilers) never branches on
    /// storage format.
    ///
    /// Extends <c>IDisposable</c> because zip archives hold native OS
    /// file handles that must be released on mod unload. Folder providers
    /// implement <c>Dispose</c> as a no-op.
    ///
    /// <c>GetFileSystemPath</c> returns <c>null</c> for zip providers —
    /// Godot's <c>ResourceLoader</c> and <c>PackedScene</c> loading
    /// require real filesystem paths, so callers that depend on these
    /// (e.g. <c>EffectFactory</c>) must guard against zip-only mods.
    /// </summary>
    public interface IModFileProvider : IDisposable
    {
        /// <summary>Short name/ID of this mod (for logging and lookup).</summary>
        string Mod { get; }

        /// <summary>Returns true if the given path exists inside this mod.</summary>
        bool FileExists(string path);

        /// <summary>
        /// Enumerates all files and directories immediately under the
        /// given directory. Directories have a trailing <c>/</c> to let
        /// recursive callers branch without additional stat calls.
        /// </summary>
        IEnumerable<string> ListFiles(string directory = "");

        /// <summary>Reads a text file as UTF-8 string, or null if not found.</summary>
        string ReadFileText(string path);

        /// <summary>Reads a binary file as byte array, or null if not found.</summary>
        byte[] ReadFileBinary(string path);

        /// <summary>Opens a seekable stream to a file inside the mod.</summary>
        Stream OpenStream(string path);

        /// <summary>
        /// Returns an absolute filesystem path for the given mod-relative
        /// path, or null if this provider cannot give filesystem access
        /// (zip mods return null here).
        /// </summary>
        string GetFileSystemPath(string path);

        /// <summary>
        /// Resolves a relative include path (<c>../shared/data.json</c>)
        /// against the current file's directory to produce a canonical
        /// mod-relative path.
        /// </summary>
        string ResolveIncludePath(string includePath, string currentPath);
    }
}
