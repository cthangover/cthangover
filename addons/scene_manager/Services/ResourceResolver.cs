#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Cthangover.Core.Utils;
using Godot;

namespace SceneManagerAddon
{
    public static class ResourceResolver
    {
        private static string _projectPath;
        private static string ProjectPath => _projectPath ??= ProjectSettings.GlobalizePath("res://");

        private static HashSet<string> _textureExtensions;
        private static HashSet<string> GetTextureExtensions()
        {
            if (_textureExtensions != null)
                return _textureExtensions;

            var configPath = Path.Combine(ProjectPath, "config", "mod_config.json");
            if (File.Exists(configPath))
            {
                try
                {
                    var json = File.ReadAllText(configPath);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("texture_extensions", out var exts) &&
                        exts.ValueKind == JsonValueKind.Array)
                    {
                        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var ext in exts.EnumerateArray())
                            set.Add(ext.GetString());
                        _textureExtensions = set;
                        return _textureExtensions;
                    }
                }
                catch { }
            }

            _textureExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".png", ".jpg", ".jpeg" };
            return _textureExtensions;
        }

        public static HashSet<string> GetAllBackgroundIds(List<ModSceneInfo> mods)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var modsPath = Path.Combine(ProjectPath, "mods");
            if (!Directory.Exists(modsPath)) return ids;

            var exts = GetTextureExtensions();

            foreach (var mod in mods)
            {
                var bgDir = Path.Combine(modsPath, mod.ModId, "backgrounds");
                if (!Directory.Exists(bgDir)) continue;

                foreach (var file in Directory.GetFiles(bgDir, "*.*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file).ToLowerInvariant();
                    if (!exts.Contains(ext)) continue;

                    const string prefix = "backgrounds/";
                    var rel = Path.GetRelativePath(Path.Combine(modsPath, mod.ModId), file).Replace('\\', '/');
                    if (rel.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        rel = rel.Substring(prefix.Length);
                    ids.Add(Path.ChangeExtension(rel, null));
                }
            }
            return ids;
        }

        public static string ResolveBackgroundFile(string backgroundId)
        {
            var modsPath = Path.Combine(ProjectPath, "mods");
            if (!Directory.Exists(modsPath)) return null;

            var exts = GetTextureExtensions();

            foreach (var modDir in Directory.GetDirectories(modsPath))
            {
                foreach (var ext in exts)
                {
                    var path = Path.Combine(modDir, "backgrounds", backgroundId.Replace('/', Path.DirectorySeparatorChar) + ext);
                    if (File.Exists(path)) return path;
                }
            }
            return null;
        }

        public static HashSet<string> GetRegisteredSceneNames(List<ModSceneInfo> mods)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mod in mods)
                foreach (var scene in mod.Scenes)
                    names.Add(scene.Name);
            return names;
        }

        public static HashSet<string> GetAllLocaleKeys(List<ModSceneInfo> mods)
        {
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var modsPath = Path.Combine(ProjectPath, "mods");
            if (!Directory.Exists(modsPath)) return keys;

            foreach (var mod in mods)
            {
                var localeDir = Path.Combine(modsPath, mod.ModId, "locale");
                if (!Directory.Exists(localeDir)) continue;

                foreach (var pf in Directory.GetFiles(localeDir, "*.properties", SearchOption.TopDirectoryOnly))
                {
                    foreach (var line in File.ReadAllLines(pf))
                    {
                        var t = line.Trim();
                        if (t.Length == 0 || t.StartsWith("#")) continue;
                        var eq = t.IndexOf('=');
                        if (eq > 0) keys.Add(t.Substring(0, eq).Trim());
                    }
                }
            }
            return keys;
        }

        public static HashSet<string> GetAllQuestIds(List<ModSceneInfo> mods)
        {
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var modsPath = Path.Combine(ProjectPath, "mods");
            if (!Directory.Exists(modsPath)) return ids;

            foreach (var mod in mods)
            {
                var qDir = Path.Combine(modsPath, mod.ModId, "quests");
                if (!Directory.Exists(qDir)) continue;
                foreach (var f in Directory.GetFiles(qDir, "*.json", SearchOption.AllDirectories))
                {
                    try
                    {
                        var jsonText = File.ReadAllText(f);
                        using var doc = JsonDocument.Parse(jsonText);
                        if (doc.RootElement.TryGetProperty("Items", out var items) &&
                            items.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in items.EnumerateArray())
                            {
                                if (item.TryGetProperty("Id", out var id) && id.ValueKind == JsonValueKind.String)
                                    ids.Add(id.GetString());
                            }
                        }
                    }
                    catch(Exception ex)
                    {
                        GameLogger.Log("GD-PLUGIN", ex.StackTrace, LogLevel.Error);
                    }
                }
            }
            return ids;
        }
    }
}
#endif
