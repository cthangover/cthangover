using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Cthangover.Core.Mods.Providers;
using Godot;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    public class ModRegistry : IModRegistry
    {
        private static readonly Lazy<ModRegistry> instance = new(() => new ModRegistry());
        public static ModRegistry Instance => instance.Value;

        private readonly Dictionary<string, IModInfo> mods = new();
        private bool initialized;

        private ModRegistry()
        {
            Mods = mods;
        }

        public bool IsInitialized => initialized;

        public IReadOnlyDictionary<string, IModInfo> Mods { get; }

        public void Initialize()
        {
            if (initialized)
                return;

            initialized = true;

            var modsRoot = ResolveModsRoot();

            GameLogger.Log("MODS_REGISTRY", $"ModRegistry.Initialize: modsRoot='{modsRoot}', exists={modsRoot != null}");

            if (modsRoot == null)
            {
                GameLogger.Log("MODS_REGISTRY", "Mods root not found, skipping mod loading", LogLevel.Error);
                GameLogger.Log("MODS_REGISTRY", $"ModRegistry.Initialize: END (no mods root), {mods.Count} mod(s)");
                return;
            }

            ScanDirectory(modsRoot);
            ModAssemblyLoader.LoadPrecompiledDlls(mods.Values);
            ModCompiler.LoadModCode(mods);

            GameLogger.Log("MODS_REGISTRY", $"ModRegistry.Initialize: loaded {mods.Count} mod(s) from {modsRoot}");
            foreach (var kvp in mods)
                GameLogger.Log("MODS_REGISTRY", $"  [{kvp.Key}] '{kvp.Value.Name}' provider={kvp.Value.FileProvider.GetType().Name}", LogLevel.Debug);
        }

        public void Reload()
        {
            initialized = false;
            mods.Clear();
            Initialize();
        }

        public IModInfo GetMod(string id)
        {
            Initialize();
            mods.TryGetValue(id, out var mod);
            return mod;
        }

        private static string ResolveModsRoot()
        {
            var userMods = ProjectSettings.GlobalizePath("user://mods/");
            if (Directory.Exists(userMods))
                return userMods;

            var resMods = ProjectSettings.GlobalizePath("res://mods/");
            if (Directory.Exists(resMods))
                return resMods;

            return null;
        }

        private void ScanDirectory(string modsRoot)
        {
            try
            {
                foreach (var entry in Directory.EnumerateFileSystemEntries(modsRoot))
                {
                    var entryName = Path.GetFileName(entry);
                    if (entryName == ".gdignore" || entryName == ".gitkeep")
                        continue;

                    var fullPath = entry.Replace('\\', '/');

                    if (Directory.Exists(fullPath))
                        LoadFolderMod(fullPath, entryName);
                    else if (entryName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        LoadZipMod(fullPath, entryName);
                }
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_REGISTRY", $"Failed to scan directory '{modsRoot}': {ex.Message}", LogLevel.Error);
            }
        }

        private void LoadFolderMod(string folderPath, string folderName)
        {
            try
            {
                var provider = new FolderModFileProvider(folderPath, folderName);
                var manifest = ReadManifest(provider);

                if (manifest == null)
                {
                    GameLogger.Log("MODS_REGISTRY", $"Skipping folder '{folderName}': no manifest found", LogLevel.Error);
                    provider.Dispose();
                    return;
                }

                var id = folderName;
                mods[id] = new ModInfo
                {
                    Id = id,
                    Name = manifest.Name,
                    Author = manifest.Author,
                    Description = manifest.Description,
                    FileProvider = provider,
                    Manifest = manifest,
                };
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_REGISTRY", $"Failed to load folder mod '{folderName}': {ex.Message}", LogLevel.Error);
            }
        }

        private void LoadZipMod(string zipPath, string zipFileName)
        {
            try
            {
                var provider = new ZipModFileProvider(zipPath, zipFileName);
                var manifest = ReadManifest(provider);

                if (manifest == null)
                {
                    GameLogger.Log("MODS_REGISTRY", $"Skipping zip '{zipFileName}': no manifest found", LogLevel.Warning);
                    provider.Dispose();
                    return;
                }

                var id = Path.GetFileNameWithoutExtension(zipFileName);
                mods[id] = new ModInfo
                {
                    Id = id,
                    Name = manifest.Name,
                    Author = manifest.Author,
                    Description = manifest.Description,
                    FileProvider = provider,
                    Manifest = manifest,
                };

                GameLogger.Log("MODS_REGISTRY", $"Loaded mod '{id}' from zip ({manifest.Name})");
            }
            catch (Exception ex)
            {
                GameLogger.Log("MODS_REGISTRY", $"Failed to load zip mod '{zipFileName}': {ex.Message}", LogLevel.Error);
            }
        }

        private static ModManifest ReadManifest(IModFileProvider provider)
        {
            if (!provider.FileExists("manifest.json"))
                return null;
            
            var json = provider.ReadFileText("manifest.json");
            if (string.IsNullOrEmpty(json))
                return null;
            
            try
            {
                return JsonSerializer.Deserialize<ModManifest>(json);
            }
            catch (JsonException ex)
            {
                GameLogger.Log("MODS_REGISTRY", $"Failed to parse manifest.json: {ex.Message}", LogLevel.Error);
            }
            return null;
        }

    }
}
