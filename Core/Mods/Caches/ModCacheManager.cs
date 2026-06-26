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
    /// <c>user://mod_cache/shaders/</c> before loading.
    ///
    /// The extraction step also copies <c>.gdshaderinclude</c> files
    /// alongside their owning <c>.gdshader</c>, because Godot resolves
    /// shader includes relative to the shader file's location on disk.
    /// Without this co-location, any shader using an include directive
    /// would fail to compile.
    ///
    /// Extraction is idempotent — <c>IsShaderCached</c> checks before
    /// writing — so repeated calls during development don't overwrite
    /// unchanged files and trigger unnecessary shader recompilation.
    /// </summary>
    public static class ModCacheManager
    {
        private const string ShaderGroupDir = "shaders";

        /// <summary>Returns the globalised cache root path from <c>ModConfig</c>.</summary>
        public static string GetCacheRoot()
        {
            return ProjectSettings.GlobalizePath(ModConfig.Instance.Cache.Root);
        }

        /// <summary>Directory path where a specific shader's files are cached.</summary>
        public static string GetShaderCacheDir(string shaderName)
        {
            return Path.Combine(GetCacheRoot(), ShaderGroupDir, shaderName);
        }

        /// <summary>Full file path where a specific shader is expected to be cached.</summary>
        public static string GetShaderCachePath(string shaderName)
        {
            return Path.Combine(GetShaderCacheDir(shaderName), shaderName + ".gdshader");
        }

        /// <summary>True if the shader already exists in the on-disk cache.</summary>
        public static bool IsShaderCached(string shaderName)
        {
            return File.Exists(GetShaderCachePath(shaderName));
        }

        /// <summary>
        /// Extracts shader files from a mod (typically a zip mod) to the
        /// on-disk cache so Godot can load them. Shader includes are
        /// copied alongside each shader because Godot resolves include
        /// directives relative to the shader's location.
        /// </summary>
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
                var targetDir = GetShaderCacheDir(shaderName);

                if (IsShaderCached(shaderName))
                    continue;

                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                var shaderBytes = provider.ReadFileBinary(entry);
                if (shaderBytes != null)
                {
                    var shaderPath = Path.Combine(targetDir, relativeName);
                    File.WriteAllBytes(shaderPath, shaderBytes);
                }

                foreach (var (incName, incBytes) in includeData)
                {
                    var incPath = Path.Combine(targetDir, incName);
                    if (!File.Exists(incPath))
                        File.WriteAllBytes(incPath, incBytes);
                }

                GameLogger.Log("CACHE", $"Cached shader '{shaderName}' from mod '{modId}'");
            }
        }
    }
}
