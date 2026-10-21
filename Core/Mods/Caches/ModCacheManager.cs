using System;
using System.Collections.Generic;
using System.IO;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods.Caches
{
    /// <summary>
    /// On-disk cache manager for shader files extracted from zip mods.
    /// Godot's <c>GD.Load&lt;Shader&gt;</c> requires a real filesystem
    /// path — it cannot read shader source from a byte array or a zip
    /// stream — so shaders shipped inside zip mods must be extracted to
    /// <c>user://mod_cache/shaders/&lt;mod&gt;/</c> before loading. The copy
    /// belongs to the mod that shipped it: a same-named shader of another
    /// mod lives in its own folder, and the resolver decides which mod wins.
    ///
    /// The extraction step also copies <c>.gdshaderinclude</c> files
    /// alongside their owning <c>.gdshader</c>, because Godot resolves
    /// shader includes relative to the shader file's location on disk.
    /// Without this co-location, any shader using an include directive
    /// would fail to compile.
    ///
    /// Extraction follows the content: a file is rewritten only when its bytes
    /// differ, so an updated mod gets its new shader on the next launch while
    /// unchanged files are not touched (no needless shader recompilation).
    /// </summary>
    public static class ModCacheManager
    {
        private const string ShaderGroupDir = "shaders";

        /// <summary>Returns the globalised cache root path from <c>ModConfig</c>.</summary>
        public static string GetCacheRoot()
        {
            if (!GameEnvironment.IsRuntime)
                return Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                    "cthangover", "mod_cache");

            return ProjectSettings.GlobalizePath(ModConfig.Instance.Cache.Root);
        }

        /// <summary>Directory path where a specific shader's files are cached.</summary>
        public static string GetShaderCacheDir(string modId, string shaderName)
        {
            return Path.Combine(GetCacheRoot(), ShaderGroupDir, modId, shaderName);
        }

        /// <summary>Full file path where a specific shader is expected to be cached.</summary>
        public static string GetShaderCachePath(string modId, string shaderName)
        {
            return Path.Combine(GetShaderCacheDir(modId, shaderName), shaderName + ".gdshader");
        }

        /// <summary>True if the shader already exists in the on-disk cache.</summary>
        public static bool IsShaderCached(string modId, string shaderName)
        {
            return File.Exists(GetShaderCachePath(modId, shaderName));
        }

        /// <summary>
        /// Extracts shader files from a mod (typically a zip mod) to the
        /// on-disk cache so Godot can load them. Shader includes are
        /// copied alongside each shader because Godot resolves include
        /// directives relative to the shader's location.
        /// </summary>
        private static bool WriteIfChanged(string path, byte[] bytes)
        {
            if (bytes == null || (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)))
                return false;
            File.WriteAllBytes(path, bytes);
            return true;
        }

        public static void ExtractShaders(string modId, IModFileProvider provider)
        {
            var files = provider.ListFiles(ShaderGroupDir);
            if (files == null)
                return;

            var shaderExts = ModConfig.Instance.GetShaderExtensionSet();
            var includeData = new Dictionary<string, byte[]>();
            var shaderEntries = new List<string>();

            foreach (var entry in files)
            {
                if (entry.EndsWith("/"))
                    continue;

                var relativeName = entry.Substring(ShaderGroupDir.Length + 1);
                var dotIndex = relativeName.LastIndexOf('.');
                if (dotIndex <= 0)
                    continue;

                var ext = relativeName.Substring(dotIndex);
                if (!shaderExts.Contains(ext))
                    continue;

                if (ext == ".gdshaderinclude")
                {
                    var bytes = provider.ReadFileBinary(entry);
                    if (bytes != null)
                        includeData[relativeName] = bytes;
                }
                else
                {
                    shaderEntries.Add(entry);
                }
            }

            foreach (var entry in shaderEntries)
            {
                var relativeName = entry.Substring(ShaderGroupDir.Length + 1);
                var dotIndex = relativeName.LastIndexOf('.');
                var shaderName = relativeName.Substring(0, dotIndex);
                var targetDir = GetShaderCacheDir(modId, shaderName);
                Directory.CreateDirectory(targetDir);

                var changed = WriteIfChanged(Path.Combine(targetDir, relativeName), provider.ReadFileBinary(entry));
                foreach (var (incName, incBytes) in includeData)
                    changed |= WriteIfChanged(Path.Combine(targetDir, incName), incBytes);

                if (changed)
                    GameLogger.Log("CACHE", $"Cached shader '{shaderName}' from mod '{modId}'");
            }
        }
    }
}
