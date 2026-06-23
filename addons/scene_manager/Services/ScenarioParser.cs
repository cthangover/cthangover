#if TOOLS
using System;
using System.Collections.Generic;
using System.IO;

namespace SceneManagerAddon
{
    public static class ScenarioParser
    {
        public static ScenarioDefInfo Parse(string filePath, string modDir)
        {
            try
            {
                var text = File.ReadAllText(filePath);
                var metaEnd = text.IndexOf("\n---");
                var content = metaEnd >= 0 ? text.Substring(metaEnd + 4) : text;
                var metaText = metaEnd >= 0 ? text.Substring(0, metaEnd) : "";

                var info = new ScenarioDefInfo
                {
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    FilePath = Path.GetRelativePath(modDir, filePath).Replace('\\', '/'),
                    AbsoluteFilePath = filePath,
                    RawText = text
                };

                foreach (var line in metaText.Split('\n'))
                {
                    var t = line.Trim();
                    if (t.Length == 0 || t.StartsWith("#")) continue;
                    var ci = t.IndexOf(':');
                    if (ci <= 0) continue;

                    var key = t.Substring(0, ci).Trim().ToLowerInvariant();
                    var val = t.Substring(ci + 1).Trim();

                    switch (key)
                    {
                        case "scene": info.SceneName = val; break;
                        case "priority": int.TryParse(val, out var p); info.Priority = p; break;
                        case "condition": info.Condition = val; break;
                    }
                }

                ExtractReferences(info, content);
                return info;
            }
            catch { return null; }
        }

        private static void ExtractReferences(ScenarioDefInfo info, string content)
        {
            var lines = content.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#") || line.StartsWith(":")) continue;

                var tokens = Tokenize(line);
                if (tokens.Count == 0) continue;

                var cmd = tokens[0].ToLowerInvariant();
                var positional = new List<string>();
                var named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                for (int t = 1; t < tokens.Count; t++)
                {
                    var tk = tokens[t];
                    if (tk == "->") { t++; continue; }
                    var eq = tk.IndexOf('=');
                    if (eq > 0)
                        named[tk.Substring(0, eq).ToLowerInvariant()] = tk.Substring(eq + 1);
                    else
                        positional.Add(tk);
                }

                switch (cmd)
                {
                    case "background":
                        if (positional.Count > 0) info.BackgroundRefs.Add(positional[0]);
                        break;
                    case "switch_scene":
                        if (positional.Count > 0) info.SwitchSceneTargets.Add(positional[0]);
                        break;
                }

                if (named.TryGetValue("key", out var lk)) info.LocaleKeys.Add(lk);
                if (named.TryGetValue("first", out var f)) info.AvatarKeys.Add(f);
                if (named.TryGetValue("second", out var s)) info.AvatarKeys.Add(s);
                if (named.TryGetValue("quest_id", out var qid)) info.QuestRefs.Add(qid);
            }
        }

        private static List<string> Tokenize(string line)
        {
            var result = new List<string>();
            var i = 0;
            while (i < line.Length)
            {
                if (char.IsWhiteSpace(line[i])) { i++; continue; }
                if (line[i] == '"')
                {
                    i++;
                    var start = i;
                    while (i < line.Length && line[i] != '"')
                    {
                        if (line[i] == '\\' && i + 1 < line.Length) i++;
                        i++;
                    }
                    result.Add(line.Substring(start, i - start));
                    if (i < line.Length) i++;
                    continue;
                }
                var tok = i;
                while (i < line.Length && !char.IsWhiteSpace(line[i])) i++;
                result.Add(line.Substring(tok, i - tok));
            }
            return result;
        }
    }
}
#endif
