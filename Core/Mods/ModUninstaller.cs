using System;
using System.IO;
using Cthangover.Core.Utils;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// Deletes an installed mod and cleans up every piece of per-mod state that
    /// would otherwise outlive it. Deletion is intentionally conservative: only
    /// mods whose files live in a writable mods root (the player's
    /// <c>user://mods</c> in runtime, the project <c>mods/</c> folder in the
    /// editor) can be removed — shipped <c>res://mods</c> content is never
    /// touched.
    /// <para/>
    /// Running mods are not unloaded: like enable/disable and install, deletion
    /// takes full effect after a restart. The caller is expected to be a
    /// pre-game UI (the mod manager) where no gameplay code reads the deleted
    /// mod's files. The provider is disposed before the delete so zip-backed
    /// mods release their file handle on Windows.
    /// </summary>
    public static class ModUninstaller
    {
        /// <summary>
        /// True when the mod is known, lives under a writable mods root and can
        /// therefore be deleted by <see cref="Uninstall"/>.
        /// </summary>
        public static bool CanUninstall(string modId)
            => CanUninstall(ModRegistry.Instance.GetMod(modId));

        /// <summary>Provider overload used by tests and callers that already resolved the mod.</summary>
        public static bool CanUninstall(IModInfo modInfo)
        {
            var rootPath = modInfo?.FileProvider?.RootPath;
            if (string.IsNullOrEmpty(rootPath))
                return false;

            return IsWritableModsRoot(GetParentPath(rootPath));
        }

        /// <summary>
        /// Removes the mod's files and all persisted per-mod state. Returns false
        /// (with a logged reason) when the mod is unknown, not deletable, or the
        /// filesystem refuses the delete.
        /// </summary>
        public static bool Uninstall(string modId)
        {
            var modInfo = ModRegistry.Instance.GetMod(modId);
            if (modInfo == null)
            {
                GameLogger.Log("UNINSTALLER", $"Uninstall('{modId}'): mod not found", LogLevel.Error);
                return false;
            }

            if (!CanUninstall(modInfo))
            {
                GameLogger.Log("UNINSTALLER", $"Uninstall('{modId}'): mod is not in a writable mods root", LogLevel.Warning);
                return false;
            }

            var rootPath = modInfo.FileProvider.RootPath;
            modInfo.FileProvider.Dispose();

            if (!DeleteModFiles(rootPath))
            {
                GameLogger.Log("UNINSTALLER", $"Uninstall('{modId}'): failed to delete '{rootPath}'", LogLevel.Error);
                return false;
            }

            DisabledModsStore.Remove(modId);
            ModLoadOrderStore.Remove(modId);
            ModSettingsRegistry.DeleteModSettings(modId);
            ModCompiler.InvalidateCache(modId);

            // Best-effort collectible unload: if no scene/UI objects hold the
            // mod's types (the mod-manager UI is a pre-game context), the
            // assemblies are released right here; otherwise the restart note
            // below stays the honest statement.
            var unloadDiagnostic = ModAssemblyLoader.UnloadMod(modId);
            var unloadNote = unloadDiagnostic == null
                ? "assemblies released"
                : $"restart required ({unloadDiagnostic})";

            GameLogger.Log("UNINSTALLER", $"Uninstalled mod '{modId}' from '{rootPath}'; {unloadNote}");
            return true;
        }

        /// <summary>
        /// Deletes a mod's directory or zip archive located at
        /// <paramref name="rootPath"/>. Exposed separately so the raw filesystem
        /// operation can be tested without a live registry.
        /// </summary>
        public static bool DeleteModFiles(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath))
                return false;

            try
            {
                if (Directory.Exists(rootPath))
                {
                    Directory.Delete(rootPath, recursive: true);
                    return true;
                }

                if (File.Exists(rootPath))
                {
                    File.Delete(rootPath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                GameLogger.Log("UNINSTALLER", $"DeleteModFiles('{rootPath}') failed: {ex.Message}", LogLevel.Error);
                return false;
            }

            return false;
        }

        private static bool IsWritableModsRoot(string rootPath)
        {
            if (string.IsNullOrEmpty(rootPath))
                return false;

            var normalized = NormalizePath(rootPath);
            foreach (var root in ModRegistry.AllModsRoots)
            {
                if (!string.Equals(NormalizePath(root.Path), normalized, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (GameEnvironment.IsRuntime && root.Priority > 0)
                    return false;

                return true;
            }

            return false;
        }

        private static string GetParentPath(string path)
        {
            var trimmed = path.TrimEnd('/', '\\');
            var parent = Path.GetDirectoryName(trimmed);
            return string.IsNullOrEmpty(parent) ? trimmed : parent;
        }

        private static string NormalizePath(string path)
        {
            var slashed = (path ?? string.Empty).Replace('\\', '/').TrimEnd('/');
            try
            {
                return Path.GetFullPath(slashed).Replace('\\', '/').TrimEnd('/');
            }
            catch (Exception)
            {
                return slashed;
            }
        }
    }
}
