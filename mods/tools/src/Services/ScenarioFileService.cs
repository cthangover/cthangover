using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cthangover.Core.Scenarios;
using Cthangover.Core.Utils;
using Godot;

namespace Cthangover.Tools.Services
{
    /// <summary>Parsed metadata from a scenario file header (before the <c>---</c> separator).</summary>
    public sealed class ScenarioMetadata
    {
        /// <summary>Target scene name for the scenario.</summary>
        public string Scene { get; set; } = "-";
        /// <summary>Priority level of the scenario.</summary>
        public string Priority { get; set; } = "-";
        /// <summary>Condition string for the scenario to activate.</summary>
        public string Condition { get; set; } = "-";
    }

    /// <summary>Collected references extracted from scenario command lines for the info panel.</summary>
    public sealed class ScenarioReferences
    {
        /// <summary>Scene switch targets referenced in commands.</summary>
        public List<string> SwitchTargets { get; set; } = new();
        /// <summary>Localisation keys found in named parameters.</summary>
        public List<string> LocaleKeys { get; set; } = new();
        /// <summary>Quest IDs referenced in named <c>quest_id</c> parameters.</summary>
        public List<string> QuestRefs { get; set; } = new();
        /// <summary>Background IDs referenced in positional parameters of background-related commands.</summary>
        public List<string> BackgroundRefs { get; set; } = new();
    }

    /// <summary>Describes a single scenario file found during scanning.</summary>
    public sealed class ScenarioFileInfo
    {
        /// <summary>Absolute path on disk.</summary>
        public string AbsolutePath { get; set; }
        /// <summary>Filename without extension.</summary>
        public string Name { get; set; }
        /// <summary>Directory path relative to the mod's <c>scenarios/</c> folder.</summary>
        public string RelativeDir { get; set; }
    }

    /// <summary>A group of scenario files within the same subdirectory under <c>scenarios/</c>.</summary>
    public sealed class ScenarioGroup
    {
        /// <summary>Relative directory path (empty for root-level files).</summary>
        public string DirectoryPath { get; set; }
        /// <summary>Files in this group, sorted by name.</summary>
        public List<ScenarioFileInfo> Files { get; set; } = new();
    }

    /// <summary>Represents all scenario files for one mod, grouped by subdirectory.</summary>
    public sealed class ModScenarioTree
    {
        /// <summary>The mod's identifier.</summary>
        public string ModId { get; set; }
        /// <summary>Groups of scenario files within this mod.</summary>
        public List<ScenarioGroup> Groups { get; set; } = new();
    }

    /// <summary>
    /// Static service for discovering, reading, writing, and analysing scenario files.
    /// Scans all <c>mods/*/scenarios/**/*.scenario</c> files in the project, groups
    /// them by mod and subdirectory, parses YAML-like metadata blocks, and extracts
    /// referenced background/scene/locale/quest identifiers. Used by
    /// <see cref="ScenarioEditorWindow"/> for the file tree and info panel.
    /// </summary>
    public static class ScenarioFileService
    {
        /// <summary>
        /// Recursively scans <c>res://mods/*/scenarios/</c> for <c>.scenario</c> files.
        /// Groups results by mod ID and subdirectory. Returns an empty list if the
        /// mods directory does not exist.
        /// </summary>
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

        /// <summary>
        /// Parses the header block (before first <c>---</c> line) in YAML-like
        /// <c>key: value</c> format to extract scene, priority, and condition.
        /// </summary>
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

        /// <summary>
        /// Walks the body of the scenario (after <c>---</c>) and extracts references:
        /// background IDs, scene switch targets, locale keys, and quest IDs.
        /// Uses <see cref="ScenarioSyntaxService.ExtractCommandParams"/> to tokenise
        /// each line and <see cref="ScenarioCommandStrategyFactory"/> metadata to
        /// classify positional parameters.
        /// </summary>
        /// <param name="text">Full scenario file text including header.</param>
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

        /// <summary>Reads the entire file at <paramref name="filePath"/> as a UTF-8 string.</summary>
        public static string ReadAllText(string filePath)
        {
            return File.ReadAllText(filePath);
        }

        /// <summary>Writes <paramref name="text"/> to <paramref name="filePath"/>, overwriting existing content.</summary>
        public static void WriteAllText(string filePath, string text)
        {
            File.WriteAllText(filePath, text);
        }

        /// <summary>Converts an absolute file path to a path relative to <c>res://</c>, using forward slashes.</summary>
        public static string GetRelativePath(string absolutePath)
        {
            var projectPath = ProjectSettings.GlobalizePath("res://");
            return Path.GetRelativePath(projectPath, absolutePath).Replace('\\', '/');
        }
    }
}
