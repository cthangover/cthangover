using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Cthangover.Core.Mods;

namespace Cthangover.LocaleAtlas
{
    /// <summary>
    /// Builds the localization key map. Pure .NET over
    /// <see cref="IModFileProvider"/> (safe on a worker thread, testable with
    /// in-memory providers). Declarations are parsed exactly like
    /// <c>LocaleLoader</c> parses them (<c>locale/&lt;lang&gt;.properties</c> or
    /// <c>locale/*_&lt;lang&gt;.properties</c>, only the top level of
    /// <c>locale/</c>, <c>#</c> comments, first <c>=</c> splits, both sides
    /// trimmed). References come from:
    /// <list type="bullet">
    /// <item><c>.scenario</c> — <c>key=</c> (the quoted literal is the fallback text);</item>
    /// <item>C# — <c>Translate("…")</c>/<c>Tr("…")</c> (any receiver) and <c>Locale = "…"</c>;</item>
    /// <item>JSON data — key-shaped values of text fields (<c>Name</c>, <c>Description</c>,
    /// <c>locale</c>, <c>title</c>, …) and any <c>"@key"</c> value;</item>
    /// <item><c>.tscn</c> — key-shaped <c>text</c>/<c>tooltip_text</c>/<c>placeholder_text</c>/<c>title</c>,
    /// which Godot auto-translates;</item>
    /// <item>manifest — <c>mod_&lt;id&gt;_name</c>/<c>_desc</c> used by the mod manager
    /// (optional: the manifest text is the fallback).</item>
    /// </list>
    /// Collection packages come as sources named <see cref="CollectionSourcePrefix"/>
    /// + id: their <c>locale/</c> files are declarations like a mod's, and their
    /// <c>collection.json</c> is checked for inline <c>locale</c> entries — a
    /// literal <c>name</c>/<c>description</c> is the <c>en</c> text, every other
    /// project language needs a <c>locale</c> entry.
    /// </summary>
    public static class LocaleAtlasBuilder
    {
        /// <summary>Source id prefix of a collection package (<c>collection:&lt;id&gt;</c>), as opposed to a mod.</summary>
        public const string CollectionSourcePrefix = "collection:";

        /// <summary>Language of literal collection texts (fallback in <c>ModCollection.GetLocalizedName</c>).</summary>
        public const string CollectionBaseLanguage = "en";

        private const string KeyPattern = @"[a-z_][A-Za-z0-9_]*(?:/[A-Za-z0-9_.\-]+)+";

        private static readonly Regex ScenarioKey = new(@"\bkey=(\S+)", RegexOptions.Compiled);
        private static readonly Regex ScenarioLiteral = new(@"^\s*[a-z_]+\s+""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);
        private static readonly Regex CodeTranslate = new(@"\b(?:Translate|Tr)\(\s*""(" + KeyPattern + @")""", RegexOptions.Compiled);
        private static readonly Regex CodeLocale = new(@"\bLocale\s*=\s*""(" + KeyPattern + @")""", RegexOptions.Compiled);
        private static readonly Regex SceneText = new(@"^(?:text|tooltip_text|placeholder_text|title) = ""(" + KeyPattern + @")""\s*$", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex JsonTextField = new(@"""(?:Name|Description|Title|Text|Label|locale|title|description|text|label|Key)""\s*:\s*""(" + KeyPattern + @")""", RegexOptions.Compiled);
        // The '@' prefix itself marks a key (CollectionText), so a key without '/' counts too.
        private static readonly Regex JsonAtKey = new(@"""@([A-Za-z0-9_][A-Za-z0-9_./\-]*)""", RegexOptions.Compiled);
        private static readonly Regex Placeholder = new(@"\{[A-Za-z0-9_]*\}", RegexOptions.Compiled);
        private static readonly Regex Cyrillic = new(@"\p{IsCyrillic}", RegexOptions.Compiled);
        private static readonly Regex FileExtension = new(@"\.(png|jpe?g|webp|svg|ogg|wav|mp3|tscn|tres|json|scenario|gdshader|cs|gd)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Languages written in Latin script: a Cyrillic value there is almost always an untranslated copy.</summary>
        private static readonly HashSet<string> LatinLanguages = new(StringComparer.OrdinalIgnoreCase) { "en", "de", "fr", "es", "it", "pt", "pl" };

        public static LocaleAtlasResult Build(IReadOnlyList<(string ModId, IModFileProvider Provider)> mods)
        {
            var result = new LocaleAtlasResult();
            var collections = new List<(string ModId, string File, JsonElement Root)>();
            var documents = new List<JsonDocument>();
            try
            {
                foreach (var (modId, provider) in mods)
                {
                    if (provider == null)
                        continue;
                    result.Mods.Add(modId);
                    ScanLocaleFiles(modId, provider, result);
                    ScanReferences(modId, provider, result, collections, documents);
                    if (modId.StartsWith(CollectionSourcePrefix, StringComparison.Ordinal))
                        continue;
                    result.References.Add(new LocaleReference { Key = $"mod_{modId}_name", ModId = modId, File = "manifest.json", Via = "manifest", Optional = true });
                    result.References.Add(new LocaleReference { Key = $"mod_{modId}_desc", ModId = modId, File = "manifest.json", Via = "manifest", Optional = true });
                }

                result.Languages.AddRange(result.Files.Select(f => f.Language).Distinct(StringComparer.Ordinal).OrderBy(l => l, StringComparer.Ordinal));

                foreach (var (modId, file, root) in collections)
                    CheckCollection(modId, file, root, result);
            }
            finally
            {
                foreach (var document in documents)
                    document.Dispose();
            }

            BuildRows(result);
            Analyze(result);
            return result;
        }

        /// <summary>
        /// Language of a locale file name, matching <c>LocaleLoader</c>: <c>ru.properties</c> → ru,
        /// <c>scenarios_ru.properties</c> → ru. Null for non-<c>.properties</c> files.
        /// </summary>
        public static string LanguageOf(string fileName)
        {
            if (!fileName.EndsWith(".properties", StringComparison.OrdinalIgnoreCase))
                return null;
            var stem = fileName.Substring(0, fileName.Length - ".properties".Length);
            var underscore = stem.LastIndexOf('_');
            return underscore >= 0 ? stem.Substring(underscore + 1) : stem;
        }

        /// <summary>Sibling file of <paramref name="file"/> for another language: <c>locale/ui_en.properties</c> → <c>locale/ui_ru.properties</c>.</summary>
        public static string SiblingFile(string file, string fromLanguage, string toLanguage)
        {
            var suffix = fromLanguage + ".properties";
            return file.EndsWith(suffix, StringComparison.Ordinal)
                ? file.Substring(0, file.Length - suffix.Length) + toLanguage + ".properties"
                : $"locale/{toLanguage}.properties";
        }

        private static void ScanLocaleFiles(string modId, IModFileProvider provider, LocaleAtlasResult result)
        {
            foreach (var file in ListFiles(provider, "locale").OrderBy(f => f, StringComparer.Ordinal))
            {
                var name = Path.GetFileName(file);
                var language = LanguageOf(name);
                if (language == null)
                    continue;

                var info = new LocaleFileInfo { ModId = modId, File = file, Language = language };
                result.Files.Add(info);

                var bytes = provider.ReadFileBinary(file) ?? Array.Empty<byte>();
                info.Bytes = bytes.Length;
                var check = LocaleEncodingChecker.Check(bytes);
                info.Encoding = check.Encoding;
                info.LineEndings = check.LineEndings;
                foreach (var issue in check.Issues)
                {
                    result.Findings.Add(new LocaleFinding
                    {
                        Severity = issue.Severity, Code = issue.Code, ModId = modId, Language = language,
                        File = file, Line = issue.Line, Message = issue.Message,
                    });
                }

                info.Issues = check.Issues.Count;

                if (file.Count(c => c == '/') > 1)
                {
                    result.Findings.Add(new LocaleFinding
                    {
                        Severity = LocaleSeverity.Error, Code = "file_not_loaded", ModId = modId, Language = language, File = file,
                        Message = "LocaleLoader reads only the top level of locale/; keys in sub-folders are never loaded",
                        Fix = $"move to locale/{name}",
                    });
                }

                var lines = check.Text.Replace("\r\n", "\n").Split('\n');
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    if (line.Length == 0 || line[0] == '#')
                        continue;

                    var separator = line.IndexOf('=');
                    if (separator < 0)
                    {
                        result.Findings.Add(new LocaleFinding
                        {
                            Severity = LocaleSeverity.Warning, Code = "line_without_separator", ModId = modId, Language = language,
                            File = file, Line = i + 1, Message = $"line has no '=' and is ignored by the loader: '{Shorten(line)}'",
                        });
                        continue;
                    }

                    var key = line.Substring(0, separator).Trim();
                    if (key.Length == 0)
                    {
                        result.Findings.Add(new LocaleFinding
                        {
                            Severity = LocaleSeverity.Warning, Code = "empty_key", ModId = modId, Language = language,
                            File = file, Line = i + 1, Message = "line has an empty key and is ignored by the loader",
                        });
                        continue;
                    }

                    if (key.Any(char.IsWhiteSpace))
                    {
                        result.Findings.Add(new LocaleFinding
                        {
                            Severity = LocaleSeverity.Warning, Code = "key_whitespace", ModId = modId, Key = key, Language = language,
                            File = file, Line = i + 1, Message = "key contains whitespace; no lookup can match it",
                        });
                    }

                    info.Keys++;
                    result.Entries.Add(new LocaleEntry
                    {
                        Key = key, Language = language, Value = line.Substring(separator + 1).Trim(),
                        ModId = modId, File = file, Line = i + 1,
                    });
                }
            }
        }

        private static void ScanReferences(
            string modId,
            IModFileProvider provider,
            LocaleAtlasResult result,
            List<(string, string, JsonElement)> collections,
            List<JsonDocument> documents)
        {
            foreach (var file in ListFiles(provider, string.Empty).OrderBy(f => f, StringComparer.Ordinal))
            {
                if (IsToolingPath(file) || file.StartsWith("locale/", StringComparison.Ordinal))
                    continue;

                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension is not (".scenario" or ".cs" or ".json" or ".tscn"))
                    continue;

                var text = provider.ReadFileText(file);
                if (string.IsNullOrEmpty(text))
                    continue;
                text = text.Replace("\r\n", "\n");

                switch (extension)
                {
                    case ".scenario":
                        ScanScenario(text, file, modId, result);
                        break;
                    case ".cs":
                        text = StripCSharpComments(text);
                        AddMatches(CodeTranslate, text, file, modId, "code", result);
                        AddMatches(CodeLocale, text, file, modId, "code", result);
                        break;
                    case ".tscn":
                        AddMatches(SceneText, text, file, modId, "scene", result);
                        break;
                    case ".json":
                        var isCollection = file == "collection.json" && modId.StartsWith(CollectionSourcePrefix, StringComparison.Ordinal);
                        if (!isCollection)
                            AddMatches(JsonTextField, text, file, modId, "json", result);
                        AddMatches(JsonAtKey, text, file, modId, isCollection ? "collection" : "json", result);
                        if (isCollection)
                        {
                            try
                            {
                                var document = JsonDocument.Parse(text, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
                                documents.Add(document);
                                collections.Add((modId, file, document.RootElement));
                            }
                            catch (JsonException)
                            {
                                // Broken collection JSON is the collection loader's error, not a locale one.
                            }
                        }

                        break;
                }
            }
        }

        private static void ScanScenario(string text, string file, string modId, LocaleAtlasResult result)
        {
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                    continue;
                foreach (Match match in ScenarioKey.Matches(line))
                {
                    var literal = ScenarioLiteral.Match(line);
                    result.References.Add(new LocaleReference
                    {
                        Key = match.Groups[1].Value, ModId = modId, File = file, Line = i + 1, Via = "scenario",
                        Fallback = literal.Success ? literal.Groups[1].Value.Replace("\\\"", "\"").Replace("\\\\", "\\") : null,
                    });
                }
            }
        }

        private static void AddMatches(Regex pattern, string text, string file, string modId, string via, LocaleAtlasResult result)
        {
            foreach (Match match in pattern.Matches(text))
            {
                var key = match.Groups[1].Value;
                if (FileExtension.IsMatch(key))
                    continue; // a path, not a key
                result.References.Add(new LocaleReference { Key = key, ModId = modId, File = file, Line = LineOf(text, match.Index), Via = via });
            }
        }

        /// <summary>
        /// Every object with a literal <c>name</c> (collection, mechanic, include)
        /// must carry a <c>locale</c> entry for each project language except the
        /// base one; <c>"@key"</c> texts are covered by the key map instead.
        /// </summary>
        private static void CheckCollection(string modId, string file, JsonElement root, LocaleAtlasResult result)
        {
            void Walk(JsonElement element, string path)
            {
                if (element.ValueKind == JsonValueKind.Array)
                {
                    var index = 0;
                    foreach (var item in element.EnumerateArray())
                        Walk(item, $"{path}[{index++}]");
                    return;
                }

                if (element.ValueKind != JsonValueKind.Object)
                    return;

                if (TryGetString(element, "name", out var name) && !name.StartsWith("@", StringComparison.Ordinal))
                {
                    var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { CollectionBaseLanguage };
                    if (element.TryGetProperty("locale", out var locale) && locale.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var entry in locale.EnumerateArray())
                        {
                            if (entry.ValueKind == JsonValueKind.Object && TryGetString(entry, "lang", out var lang)
                                && (TryGetString(entry, "name", out var n) && n.Length > 0 || TryGetString(entry, "description", out var d) && d.Length > 0))
                                present.Add(lang);
                        }
                    }

                    var id = TryGetString(element, "id", out var objectId) ? objectId : name;
                    foreach (var language in result.Languages.Where(l => !present.Contains(l)))
                    {
                        result.Findings.Add(new LocaleFinding
                        {
                            Severity = LocaleSeverity.Error, Code = "collection_text_missing", ModId = modId, Key = $"{id}", Language = language,
                            File = file, Message = $"'{name}' ({path.TrimStart('/').OrEmpty("root")}) has no locale entry for '{language}'",
                            Fix = $"add {{ \"lang\": \"{language}\", \"name\": \"…\", \"description\": \"…\" }} to \"locale\"",
                        });
                    }
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (property.NameEquals("locale"))
                        continue;
                    Walk(property.Value, $"{path}/{property.Name}");
                }
            }

            Walk(root, string.Empty);
        }

        private static void BuildRows(LocaleAtlasResult result)
        {
            LocaleKeyRow Row(string key)
            {
                if (!result.Keys.TryGetValue(key, out var row))
                    result.Keys[key] = row = new LocaleKeyRow { Key = key };
                return row;
            }

            foreach (var entry in result.Entries)
            {
                var row = Row(entry.Key);
                if (!row.Declarations.TryGetValue(entry.Language, out var list))
                    row.Declarations[entry.Language] = list = new List<LocaleEntry>();
                list.Add(entry);
            }

            var localized = new HashSet<string>(result.Files.Select(f => f.ModId), StringComparer.Ordinal);
            foreach (var reference in result.References)
            {
                // Optional manifest keys of mods without any locale file are not part of the map.
                if (reference.Optional && !result.Keys.ContainsKey(reference.Key) && !localized.Contains(reference.ModId))
                    continue;
                Row(reference.Key).References.Add(reference);
            }

            foreach (var row in result.Keys.Values)
            {
                if (!row.Declared)
                    continue;
                foreach (var language in result.Languages)
                {
                    if (!row.Declarations.TryGetValue(language, out var entries))
                        row.MissingLanguages.Add(language);
                    else if (entries.Any(e => e.Value.Length == 0))
                        row.EmptyLanguages.Add(language);
                }

                foreach (var mod in row.DeclaringMods)
                {
                    var lacks = result.Languages
                        .Where(l => !row.Declarations.TryGetValue(l, out var entries) || entries.All(e => e.ModId != mod))
                        .ToList();
                    if (lacks.Count > 0)
                        row.PartialMods[mod] = lacks;
                }
            }
        }

        private static void Analyze(LocaleAtlasResult result)
        {
            foreach (var row in result.Keys.Values)
            {
                if (!row.Declared)
                {
                    AnalyzeUndeclared(row, result);
                    continue;
                }

                foreach (var (mod, lacks) in row.PartialMods)
                {
                    var own = row.Declarations.Values.SelectMany(e => e).First(e => e.ModId == mod);
                    foreach (var language in lacks)
                    {
                        var global = row.MissingLanguages.Contains(language);
                        var other = row.Declarations.TryGetValue(language, out var others) ? others.Select(e => e.ModId).Distinct().ToList() : null;
                        result.Findings.Add(new LocaleFinding
                        {
                            Severity = global ? LocaleSeverity.Error : LocaleSeverity.Warning,
                            Code = global ? "key_missing_language" : "key_partial_override",
                            ModId = mod, Key = row.Key, Language = language, File = own.File, Line = own.Line,
                            Message = global
                                ? $"declared in {string.Join(", ", row.Declarations.Keys)} but not in '{language}'"
                                : $"declared by this mod only in {string.Join(", ", row.Declarations.Values.SelectMany(e => e).Where(e => e.ModId == mod).Select(e => e.Language).Distinct())}; '{language}' comes from {string.Join(", ", other)}",
                            Fix = $"{SiblingFile(own.File, own.Language, language)}: {row.Key} = {SuggestValue(row, language, mod)}",
                        });
                    }
                }

                foreach (var entries in row.Declarations.Values)
                {
                    foreach (var entry in entries.Where(e => e.Value.Length == 0))
                    {
                        result.Findings.Add(new LocaleFinding
                        {
                            Severity = LocaleSeverity.Error, Code = "key_empty", ModId = entry.ModId, Key = row.Key, Language = entry.Language,
                            File = entry.File, Line = entry.Line, Message = "value is empty",
                            Fix = "write the text, or drop the key (and the key= reference) when the text is meant to be empty",
                        });
                    }

                    foreach (var group in entries.GroupBy(e => e.ModId).Where(g => g.Count() > 1))
                    {
                        var values = group.Select(e => e.Value).Distinct(StringComparer.Ordinal).Count();
                        var first = group.First();
                        result.Findings.Add(new LocaleFinding
                        {
                            Severity = values > 1 ? LocaleSeverity.Error : LocaleSeverity.Warning,
                            Code = values > 1 ? "key_conflict" : "key_duplicate",
                            ModId = group.Key, Key = row.Key, Language = first.Language, File = first.File, Line = first.Line,
                            Message = (values > 1 ? "declared with different values in " : "declared more than once in ")
                                      + string.Join(", ", group.Select(e => $"{e.File}:{e.Line}")),
                            Fix = "keep one declaration (scenario keys belong in scenarios_<lang>.properties)",
                        });
                    }

                    foreach (var entry in entries.Where(e => LatinLanguages.Contains(e.Language) && Cyrillic.IsMatch(e.Value)))
                    {
                        result.Findings.Add(new LocaleFinding
                        {
                            Severity = LocaleSeverity.Warning, Code = "untranslated", ModId = entry.ModId, Key = row.Key, Language = entry.Language,
                            File = entry.File, Line = entry.Line, Message = $"Cyrillic text in '{entry.Language}': '{Shorten(entry.Value)}'",
                        });
                    }
                }

                AnalyzePlaceholders(row, result);
            }
        }

        private static void AnalyzeUndeclared(LocaleKeyRow row, LocaleAtlasResult result)
        {
            foreach (var group in row.References.GroupBy(r => r.ModId))
            {
                var first = group.First();
                var optional = group.All(r => r.Optional);
                foreach (var language in result.Languages)
                {
                    result.Findings.Add(new LocaleFinding
                    {
                        Severity = optional ? LocaleSeverity.Warning : LocaleSeverity.Error,
                        Code = optional ? "key_undeclared_optional" : "key_undeclared",
                        ModId = group.Key, Key = row.Key, Language = language, File = first.File, Line = first.Line,
                        Message = optional
                            ? "not declared; the manifest text is shown instead"
                            : $"referenced ({string.Join(", ", group.Select(r => r.Via).Distinct())}, {group.Count()}×) but declared in no locale file",
                        Fix = $"{TargetFile(group.Key, first.Via, language, result)}: {row.Key} = {SuggestFromFallback(group, language)}",
                    });
                }
            }
        }

        /// <summary>Placeholders (<c>{0}</c>, <c>{name}</c>) must be the same in every language of one mod.</summary>
        private static void AnalyzePlaceholders(LocaleKeyRow row, LocaleAtlasResult result)
        {
            foreach (var mod in row.DeclaringMods)
            {
                var sets = row.Declarations.Values.SelectMany(e => e)
                    .Where(e => e.ModId == mod && e.Value.Length > 0)
                    .GroupBy(e => e.Language)
                    .Select(g => (Entry: g.First(), Set: string.Join(" ", Placeholder.Matches(g.First().Value).Select(m => m.Value).Distinct().OrderBy(v => v, StringComparer.Ordinal))))
                    .ToList();
                if (sets.Select(s => s.Set).Distinct().Count() <= 1)
                    continue;
                var entry = sets[0].Entry;
                result.Findings.Add(new LocaleFinding
                {
                    Severity = LocaleSeverity.Warning, Code = "placeholder_mismatch", ModId = mod, Key = row.Key, Language = entry.Language,
                    File = entry.File, Line = entry.Line,
                    Message = "placeholders differ between languages: " + string.Join("; ", sets.Select(s => $"{s.Entry.Language}: {s.Set.OrEmpty("none")}")),
                });
            }
        }

        private static string TargetFile(string modId, string via, string language, LocaleAtlasResult result)
        {
            var files = result.Files.Where(f => f.ModId == modId && f.Language == language).Select(f => f.File).ToList();
            if (via == "scenario")
                return $"locale/scenarios_{language}.properties";
            return files.FirstOrDefault(f => !Path.GetFileName(f).StartsWith("scenarios_", StringComparison.Ordinal))
                   ?? $"locale/{language}.properties";
        }

        private static string SuggestValue(LocaleKeyRow row, string language, string mod)
        {
            var fallback = row.References.Where(r => r.ModId == mod && r.Fallback != null).Select(r => r.Fallback).FirstOrDefault();
            if (fallback != null && MatchesScript(fallback, language))
                return fallback;
            var hint = row.Declarations
                .Where(d => d.Key != language)
                .Select(d => $"{d.Key}: {d.Value.First().Value}")
                .FirstOrDefault();
            return $"… ({Shorten(hint ?? "no text in other languages")})";
        }

        private static string SuggestFromFallback(IEnumerable<LocaleReference> references, string language)
        {
            var fallback = references.Select(r => r.Fallback).FirstOrDefault(f => !string.IsNullOrEmpty(f));
            return fallback != null && MatchesScript(fallback, language) ? fallback : "…";
        }

        /// <summary>A scenario literal can be reused as the value when its script fits the language.</summary>
        private static bool MatchesScript(string text, string language)
            => LatinLanguages.Contains(language) ? !Cyrillic.IsMatch(text) : Cyrillic.IsMatch(text) || !text.Any(char.IsLetter);

        private static bool TryGetString(JsonElement element, string name, out string value)
        {
            value = null;
            if (!element.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.String)
                return false;
            value = property.GetString() ?? string.Empty;
            return true;
        }

        private static List<string> ListFiles(IModFileProvider provider, string directory)
        {
            var result = new List<string>();
            var pending = new Stack<string>();
            pending.Push(directory);
            while (pending.Count > 0)
            {
                foreach (var entry in provider.ListFiles(pending.Pop()) ?? Enumerable.Empty<string>())
                {
                    if (entry.EndsWith("/", StringComparison.Ordinal))
                        pending.Push(entry.TrimEnd('/'));
                    else
                        result.Add(entry);
                }
            }

            return result;
        }

        /// <summary>Tests and build output are not shipped content; their literals are not references.</summary>
        private static bool IsToolingPath(string file)
            => file.StartsWith("tests/", StringComparison.Ordinal)
               || file.StartsWith("test_suites/", StringComparison.Ordinal) // mounted as their own mod (<host>__<suite>)
               || file.StartsWith("source_code/", StringComparison.Ordinal)
               || file.Contains("/bin/", StringComparison.Ordinal)
               || file.Contains("/obj/", StringComparison.Ordinal);

        private static string StripCSharpComments(string text)
        {
            var chars = text.ToCharArray();
            var i = 0;
            while (i < chars.Length)
            {
                var c = chars[i];
                var next = i + 1 < chars.Length ? chars[i + 1] : '\0';
                if (c == '/' && next == '/')
                {
                    while (i < chars.Length && chars[i] != '\n')
                        chars[i++] = ' ';
                }
                else if (c == '/' && next == '*')
                {
                    while (i < chars.Length && !(chars[i] == '*' && i + 1 < chars.Length && chars[i + 1] == '/'))
                    {
                        if (chars[i] != '\n')
                            chars[i] = ' ';
                        i++;
                    }

                    if (i < chars.Length)
                    {
                        chars[i++] = ' ';
                        chars[i++] = ' ';
                    }
                }
                else if (c == '"' || c == '\'')
                {
                    var verbatim = c == '"' && i > 0 && (chars[i - 1] == '@' || (chars[i - 1] == '$' && i > 1 && chars[i - 2] == '@'));
                    i++;
                    while (i < chars.Length && chars[i] != c && chars[i] != '\n')
                    {
                        if (!verbatim && chars[i] == '\\')
                            i++;
                        else if (verbatim && chars[i] == '"' && i + 1 < chars.Length && chars[i + 1] == '"')
                            i++;
                        i++;
                    }

                    i++;
                }
                else
                {
                    i++;
                }
            }

            return new string(chars);
        }

        private static int LineOf(string text, int index)
        {
            var line = 1;
            for (var i = 0; i < index && i < text.Length; i++)
            {
                if (text[i] == '\n')
                    line++;
            }

            return line;
        }

        internal static string Shorten(string text)
            => text.Length <= 60 ? text : text.Substring(0, 57) + "...";

        internal static string OrEmpty(this string text, string fallback)
            => string.IsNullOrEmpty(text) ? fallback : text;
    }
}
