using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Tools.Services
{
    public sealed class ScenarioMetadata
    {
        public string Scene { get; set; } = "-";
        public string Priority { get; set; } = "-";
        public string Condition { get; set; } = "-";
    }

    public sealed class ScenarioReferences
    {
        public List<string> SwitchTargets { get; set; } = new();
        public List<string> LocaleKeys { get; set; } = new();
        public List<string> QuestRefs { get; set; } = new();
        public List<string> BackgroundRefs { get; set; } = new();
    }

    public sealed class ScenarioFileInfo
    {
        public string AbsolutePath { get; set; }
        public string Name { get; set; }
        public string RelativeDir { get; set; }
    }

    public sealed class ScenarioGroup
    {
        public string DirectoryPath { get; set; }
        public List<ScenarioFileInfo> Files { get; set; } = new();
    }

    public sealed class ModScenarioTree
    {
        public string ModId { get; set; }
        public List<ScenarioGroup> Groups { get; set; } = new();
    }

    public static class ScenarioFileService
    {
        public static List<ModScenarioTree> ScanScenarioFiles()
        {
            var result = new List<ModScenarioTree>();
            var projectPath = ProjectSettings.GlobalizePath("res://");
            var modsPath = Path.Combine(projectPath, "mods");

            if (!Directory.Exists(modsPath))
                return result;

            foreach (var modDir in Directory.GetDirectories(modsPath).OrderBy(d => Path.GetFileName(d)))
            {
                var modId = Path.GetFileName(modDir);
                var scenariosDir = Path.Combine(modDir, "scenarios");
                if (!Directory.Exists(scenariosDir))
                    continue;

                var scenarioFiles = Directory.GetFiles(scenariosDir, "*.scenario", SearchOption.AllDirectories);
                if (scenarioFiles.Length == 0)
                    continue;

                var groups = scenarioFiles
                    .GroupBy(f =>
                    {
                        var relDir = Path.GetRelativePath(scenariosDir, Path.GetDirectoryName(f));
                        return relDir == "." ? "" : relDir.Replace('\\', '/');
                    })
                    .OrderBy(g => g.Key)
                    .Select(g => new ScenarioGroup
                    {
                        DirectoryPath = g.Key,
                        Files = g.OrderBy(f => Path.GetFileNameWithoutExtension(f))
                            .Select(f => new ScenarioFileInfo
                            {
                                AbsolutePath = f,
                                Name = Path.GetFileNameWithoutExtension(f),
                                RelativeDir = g.Key
                            })
                            .ToList()
                    })
                    .ToList();

                result.Add(new ModScenarioTree { ModId = modId, Groups = groups });
            }

            return result;
        }

        public static ScenarioMetadata ParseMetadata(string text)
        {
            var meta = new ScenarioMetadata();
            var metaEnd = text.IndexOf("\n---");
            var metaText = metaEnd >= 0 ? text.Substring(0, metaEnd) : "";

            foreach (var line in metaText.Split('\n'))
            {
                var t = line.Trim();
                if (t.Length == 0 || t.StartsWith("#"))
                    continue;

                var ci = t.IndexOf(':');
                if (ci <= 0)
                    continue;

                var key = t.Substring(0, ci).Trim().ToLowerInvariant();
                var val = t.Substring(ci + 1).Trim();

                switch (key)
                {
                    case "scene": meta.Scene = val; break;
                    case "priority": meta.Priority = val; break;
                    case "condition": meta.Condition = val; break;
                }
            }

            return meta;
        }

        public static ScenarioReferences ExtractReferences(string text)
        {
            var refs = new ScenarioReferences();
            var metaEnd = text.IndexOf("\n---");
            var content = metaEnd >= 0 ? text.Substring(metaEnd + 4) : text;

            foreach (var rawLine in content.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(":"))
                    continue;

                ScenarioSyntaxService.ExtractCommandParams(line, out var cmd, out var positional, out var named);

                if (string.IsNullOrEmpty(cmd))
                    continue;

                var meta = ScenarioCommandStrategyFactory.GetReferenceMetadata(cmd);
                if (meta != null && positional.Count > 0)
                {
                    switch (meta.Positional0Kind)
                    {
                        case PositionalReferenceKind.Background:
                            refs.BackgroundRefs.Add(positional[0]);
                            break;
                        case PositionalReferenceKind.Scene:
                            refs.SwitchTargets.Add(positional[0]);
                            break;
                    }
                }

                if (named.TryGetValue("key", out var lk))
                    refs.LocaleKeys.Add(lk);
                if (named.TryGetValue("quest_id", out var qid))
                    refs.QuestRefs.Add(qid);
            }

            return refs;
        }

        public static string ReadAllText(string filePath)
        {
            return File.ReadAllText(filePath);
        }

        public static void WriteAllText(string filePath, string text)
        {
            File.WriteAllText(filePath, text);
        }

        public static string GetRelativePath(string absolutePath)
        {
            var projectPath = ProjectSettings.GlobalizePath("res://");
            return Path.GetRelativePath(projectPath, absolutePath).Replace('\\', '/');
        }
    }
}
