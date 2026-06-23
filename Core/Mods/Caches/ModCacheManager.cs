using System.Collections.Generic;
using System.IO;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Core.Mods.Caches
{
    public static class ModCacheManager
    {
        private const string ShaderGroupDir = "shaders";

        public static string GetCacheRoot()
        {
            return ProjectSettings.GlobalizePath(ModConfig.Instance.Cache.Root);
        }

        public static string GetShaderCacheDir(string shaderName)
        {
            return Path.Combine(GetCacheRoot(), ShaderGroupDir, shaderName);
        }

        public static string GetShaderCachePath(string shaderName)
        {
            return Path.Combine(GetShaderCacheDir(shaderName), shaderName + ".gdshader");
        }

        public static bool IsShaderCached(string shaderName)
        {
            return File.Exists(GetShaderCachePath(shaderName));
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
