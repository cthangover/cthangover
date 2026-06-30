#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace SceneManagerAddon
{
    /// <summary>
    /// Entry point for loading all scene and scenario data from disk.
    /// Walks the <c>res://mods/</c> directory tree, parses every
    /// <c>scenes/*.json</c> into <see cref="SceneDefInfo"/> objects
    /// grouped by <see cref="ModSceneInfo"/>, then attaches
    /// <c>scenarios/*.scenario</c> files to the scene whose name
    /// matches the scenario's <c>scene:</c> meta field.
    /// </summary>
    public static class SceneDataLoader
    {
        private static string _projectPath;
        private static string ProjectPath => _projectPath ??= ProjectSettings.GlobalizePath("res://");

        /// <summary>
        /// Performs a full crawl of the <c>mods/</c> tree. For each mod
        /// directory that contains a <c>scenes/</c> subfolder, every
        /// <c>*.json</c> file is parsed; if the resulting mod has at
        /// least one valid scene it is included in the output list.
        /// Afterwards, <c>AttachScenarios</c> links each
        /// <c>*.scenario</c> file to its target scene by matching the
        /// scenario's <c>scene:</c> header against the scene's
        /// <see cref="SceneDefInfo.Name"/>.
        /// </summary>
        public static List<ModSceneInfo> LoadAll()
        {
            var mods = new List<ModSceneInfo>();
            var modsPath = Path.Combine(ProjectPath, "mods");
            if (!Directory.Exists(modsPath)) return mods;

            foreach (var modDir in Directory.GetDirectories(modsPath))
            {
                var modId = Path.GetFileName(modDir);
                var scenesDir = Path.Combine(modDir, "scenes");
                if (!Directory.Exists(scenesDir)) continue;

                var modInfo = new ModSceneInfo
                {
                    ModId = modId,
                    ModPath = "res://mods/" + modId
                };

                foreach (var jsonFile in Directory.GetFiles(scenesDir, "*.json", SearchOption.AllDirectories))
                {
                    var def = ParseSceneJson(jsonFile, modId, modDir);
                    if (def != null) modInfo.Scenes.Add(def);
                }

                if (modInfo.Scenes.Count > 0) mods.Add(modInfo);
            }

            AttachScenarios(mods);
            return mods;
        }

        private static SceneDefInfo ParseSceneJson(string filePath, string modId, string modDir)
        {
            try
            {
                var text = File.ReadAllText(filePath);
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;

                var def = new SceneDefInfo
                {
                    Name = root.TryGetProperty("name", out var n) ? n.GetString() : null,
                    ModId = modId,
                    FilePath = Path.GetRelativePath(modDir, filePath).Replace('\\', '/'),
                    RawJson = text
                };

                if (string.IsNullOrEmpty(def.Name)) return null;

                if (root.TryGetProperty("defaultBackground", out var bg))
                {
                    if (bg.ValueKind == JsonValueKind.String)
                        def.DefaultBackgrounds.Add(bg.GetString());
                    else if (bg.ValueKind == JsonValueKind.Array)
                        foreach (var item in bg.EnumerateArray())
                            if (item.ValueKind == JsonValueKind.String)
                                def.DefaultBackgrounds.Add(item.GetString());
                }

                if (root.TryGetProperty("defaultAmbient", out var amb))
                    def.DefaultAmbient = amb.GetString();
                if (root.TryGetProperty("defaultScenario", out var ds))
                    def.DefaultScenario = ds.GetString();

                return def;
            }
            catch { return null; }
        }

        private static void AttachScenarios(List<ModSceneInfo> mods)
        {
            var modsPath = Path.Combine(ProjectPath, "mods");
            if (!Directory.Exists(modsPath)) return;

            foreach (var modInfo in mods)
            {
                var modDir = Path.Combine(modsPath, modInfo.ModId);
                var scenariosDir = Path.Combine(modDir, "scenarios");
                if (!Directory.Exists(scenariosDir)) continue;

                foreach (var scFile in Directory.GetFiles(scenariosDir, "*.scenario", SearchOption.AllDirectories))
                {
                    var scenario = ScenarioParser.Parse(scFile, modDir);
                    if (scenario == null) continue;

                    var target = modInfo.Scenes.FirstOrDefault(s =>
                        string.Equals(s.Name, scenario.SceneName, StringComparison.OrdinalIgnoreCase));
                    target?.Scenarios.Add(scenario);
                }
            }
        }
    }
}
#endif
