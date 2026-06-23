using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Cthangover.Core.Localization;
using Cthangover.Core.UI.Dialog;
using Cthangover.Core.UI.Dialog.Action;
using Cthangover.Core.UI.Dialog.Action.Impls;
using Godot;

namespace Cthangover.Core.Scenarios
{
    public static class ScenarioParser
    {
        public static DialogQueue Parse(string text, ILocalizationProvider locale = null)
        {
            var dlg = new DialogQueue();
            var lines = text.Split('\n');
            ActionSelect pendingSelect = null;

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    FinalizePendingSelect(dlg, ref pendingSelect);
                    continue;
                }

                if (line.StartsWith(':'))
                {
                    FinalizePendingSelect(dlg, ref pendingSelect);
                    dlg.Point(line.Substring(1));
                    continue;
                }

                var tokens = Tokenize(line);
                if (tokens.Count == 0) continue;

                var cmd = tokens[0].ToLowerInvariant();
                tokens.RemoveAt(0);

                var positional = new List<string>();
                var named = new Dictionary<string, string>();
                var arrowTarget = "";

                for (var i = 0; i < tokens.Count; i++)
                {
                    var t = tokens[i];
                    if (t == "->")
                    {
                        if (i + 1 < tokens.Count)
                        {
                            arrowTarget = tokens[i + 1].TrimStart(':');
                            i++;
                        }
                        continue;
                    }

                    var eqIdx = t.IndexOf('=');
                    if (eqIdx > 0)
                    {
                        var name = t.Substring(0, eqIdx);
                        if (IsValidIdent(name))
                        {
                            named[name.ToLowerInvariant()] = t.Substring(eqIdx + 1);
                            continue;
                        }
                    }

                    positional.Add(t);
                }

                if (cmd == "select")
                {
                    FinalizePendingSelect(dlg, ref pendingSelect);
                    pendingSelect = new ActionSelect();
                    var promptText = positional.Count > 0 ? positional[0] : null;

                    if (named.TryGetValue("key", out var key) && locale != null)
                    {
                        var loc = locale.Get(key);
                        if (loc != null) promptText = loc;
                    }
                    if (promptText != null)
                        pendingSelect.Text = promptText;

                    if (named.TryGetValue("first", out var first))
                        pendingSelect.FirstAvatar = first;
                    if (named.TryGetValue("second", out var second))
                        pendingSelect.SecondAvatar = second;

                    continue;
                }

                if (cmd == "option")
                {
                    if (pendingSelect == null) continue;

                    var optText = positional.Count > 0 ? positional[0] : "";

                    if (named.TryGetValue("key", out var key) && locale != null)
                    {
                        var loc = locale.Get(key);
                        if (loc != null) optText = loc;
                    }

                    var goTo = arrowTarget;
                    if (string.IsNullOrEmpty(goTo))
                    {
                        if (named.TryGetValue("goto", out var gt))
                            goTo = gt;
                        else if (positional.Count > 1 && positional[1].StartsWith(':'))
                            goTo = positional[1].TrimStart(':');
                    }

                    if (pendingSelect.Variants == null)
                        pendingSelect.Variants = new List<SelectVariant>();
                    pendingSelect.Variants.Add(SelectVariant.New(optText, goTo));
                    continue;
                }

                FinalizePendingSelect(dlg, ref pendingSelect);

                var strategy = ScenarioCommandStrategyFactory.Get(cmd);
                if (strategy != null)
                    strategy.Execute(dlg, positional, named, arrowTarget, locale);
            }

            FinalizePendingSelect(dlg, ref pendingSelect);
            return dlg;
        }

        public static (Dictionary<string, string> metadata, string remainingText) ParseMetadata(string text)
        {
            var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = text.Split('\n');
            var contentStart = 0;
            var inMetadata = true;

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();

                if (!inMetadata)
                {
                    continue;
                }

                if (line == "---")
                {
                    contentStart = i + 1;
                    break;
                }

                if (string.IsNullOrEmpty(line))
                {
                    contentStart = i;
                    break;
                }

                var colonIdx = line.IndexOf(':');
                if (colonIdx > 0)
                {
                    var key = line.Substring(0, colonIdx).Trim();
                    var val = line.Substring(colonIdx + 1).Trim();
                    metadata[key] = val;
                }
                else
                {
                    contentStart = i;
                    break;
                }
            }

            var remaining = string.Join("\n", lines, contentStart, lines.Length - contentStart);
            return (metadata, remaining);
        }

        private static void FinalizePendingSelect(DialogQueue dlg, ref ActionSelect pending)
        {
            if (pending == null) return;

            if (pending.Variants == null || pending.Variants.Count == 0)
            {
                pending = null;
                return;
            }

            dlg.Queue.Add(pending);
            pending = null;
        }

        private static List<string> Tokenize(string line)
        {
            var result = new List<string>();
            var i = 0;

            while (i < line.Length)
            {
                if (char.IsWhiteSpace(line[i]))
                {
                    i++;
                    continue;
                }

                if (line[i] == '"')
                {
                    i++;
                    var start = i;
                    while (i < line.Length && line[i] != '"')
                    {
                        if (line[i] == '\\' && i + 1 < line.Length)
                            i++;
                        i++;
                    }
                    result.Add(line[start..i].Replace("\\\"", "\""));
                    if (i < line.Length) i++;
                    continue;
                }

                var tokenStart = i;
                while (i < line.Length && !char.IsWhiteSpace(line[i]))
                    i++;
                result.Add(line[tokenStart..i]);
            }

            return result;
        }

        private static bool IsValidIdent(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            for (var i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (!char.IsLetterOrDigit(c) && c != '_')
                    return false;
            }
            return true;
        }
    }
}
