using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.Testing;

namespace Cthangover.LocaleAtlas.Tests
{
    /// <summary>
    /// Pure tests of the encoding checker and the key map on in-memory mods:
    /// no scene tree, no installed mods, so they pin the rules themselves.
    /// </summary>
    public class LocaleAtlasTests
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        [ModTest("encoding/valid_utf8_is_clean")]
        static void ValidUtf8IsClean(ModTestContext ctx)
        {
            // Real Russian that starts with Р/С followed by ё, «, — must not look like mojibake.
            var check = LocaleEncodingChecker.Check(Utf8.GetBytes("a = Рёв зверя\nb = Сёстры — «Р»\nc = Hello {0}\n"));
            ModAssert.Equal("UTF-8", check.Encoding, "encoding");
            ModAssert.Equal("LF", check.LineEndings, "line endings");
            ModAssert.Equal(0, check.Issues.Count, "issues: " + string.Join("; ", check.Issues.Select(i => i.Code)));
        }

        [ModTest("encoding/bom_and_line_endings")]
        static void BomAndLineEndings(ModTestContext ctx)
        {
            var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Utf8.GetBytes("a = 1\r\nb = 2\n")).ToArray();
            var check = LocaleEncodingChecker.Check(bytes);
            ModAssert.Equal("UTF-8 BOM", check.Encoding, "encoding");
            ModAssert.Equal("mixed", check.LineEndings, "line endings");
            ModAssert.True(check.Issues.Any(i => i.Code == "utf8_bom" && i.Severity == LocaleSeverity.Warning), "BOM warning");
            ModAssert.True(check.Issues.Any(i => i.Code == "mixed_line_endings"), "mixed endings warning");
            ModAssert.False(check.Text.StartsWith("﻿", StringComparison.Ordinal), "BOM stripped from text");
        }

        [ModTest("encoding/cp1251_bytes_are_invalid_utf8")]
        static void Cp1251BytesAreInvalid(ModTestContext ctx)
        {
            // "a = Привет" saved in cp1251.
            var bytes = Encoding.ASCII.GetBytes("ok = 1\na = ").Concat(new byte[] { 0xCF, 0xF0, 0xE8, 0xE2, 0xE5, 0xF2 }).ToArray();
            var check = LocaleEncodingChecker.Check(bytes);
            ModAssert.Equal("invalid", check.Encoding, "encoding");
            var issue = check.Issues.FirstOrDefault(i => i.Code == "invalid_utf8");
            ModAssert.NotNull(issue, "invalid_utf8 reported");
            ModAssert.Equal(2, issue.Line, "line of the broken bytes");
            ModAssert.True(issue.Message.Contains("cp1251"), "cp1251 hint: " + issue.Message);
        }

        [ModTest("encoding/mojibake_is_detected")]
        static void MojibakeIsDetected(ModTestContext ctx)
        {
            // UTF-8 "Привет" shown as cp1251 and as cp1252, then saved as UTF-8 again.
            var a = LocaleEncodingChecker.Check(Utf8.GetBytes("ok = Привет\na = РџСЂРёРІРµС‚\n"));
            ModAssert.True(a.Issues.Any(i => i.Code == "mojibake_cp1251" && i.Line == 2), "cp1251 mojibake on line 2");
            ModAssert.False(a.Issues.Any(i => i.Line == 1), "clean line 1");

            var b = LocaleEncodingChecker.Check(Utf8.GetBytes("b = ÐŸÑ€Ð¸Ð²ÐµÑ‚\n"));
            ModAssert.True(b.Issues.Any(i => i.Code == "mojibake_latin1"), "cp1252 mojibake");

            var raw = Encoding.Latin1.GetString(Utf8.GetBytes("Привет"));
            var c1 = LocaleEncodingChecker.Check(Utf8.GetBytes($"b = {raw}\n"));
            ModAssert.True(c1.Issues.Any(i => i.Code is "mojibake_latin1" or "control_char"), "latin-1 mojibake (C1 controls)");

            var c = LocaleEncodingChecker.Check(Utf8.GetBytes("c = Пр�вет\n"));
            ModAssert.True(c.Issues.Any(i => i.Code == "replacement_char"), "U+FFFD");
        }

        [ModTest("encoding/utf16_is_reported")]
        static void Utf16IsReported(ModTestContext ctx)
        {
            var withBom = LocaleEncodingChecker.Check(Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("a = 1\n")).ToArray());
            ModAssert.Equal("UTF-16 LE", withBom.Encoding, "UTF-16 with BOM");
            ModAssert.True(withBom.Issues.Any(i => i.Code == "encoding_utf16"), "UTF-16 reported");

            var noBom = LocaleEncodingChecker.Check(Encoding.Unicode.GetBytes("a = 1\nb = 2\n"));
            ModAssert.True(noBom.Issues.Any(i => i.Code == "encoding_utf16_nobom"), "UTF-16 without BOM reported");
        }

        [ModTest("map/complete_keys_are_not_interesting")]
        static void CompleteKeysAreNotInteresting(ModTestContext ctx)
        {
            var mod = new MemoryProvider("m")
                .Add("locale/en.properties", "ok = OK\nmod_m_name = M\nmod_m_desc = M\n")
                .Add("locale/ru.properties", "ok = Ок\nmod_m_name = М\nmod_m_desc = М\n");
            var result = LocaleAtlasBuilder.Build(new[] { ("m", (IModFileProvider)mod) });
            ModAssert.Equal("en,ru", string.Join(",", result.Languages), "languages from file names");
            ModAssert.True(result.Keys["ok"].Complete, "key is complete");
            ModAssert.Equal(0, result.InterestingKeys.Count(), "nothing to report");
            ModAssert.Equal(0, result.Errors.Count(), "no errors");
        }

        [ModTest("map/missing_language_points_to_sibling_file")]
        static void MissingLanguagePointsToSibling(ModTestContext ctx)
        {
            var mod = new MemoryProvider("m")
                .Add("locale/ui_en.properties", "ui/only_en = Only English\nui/both = Both\n")
                .Add("locale/ui_ru.properties", "ui/both = Оба\nui/only_ru = Только русский\n");
            var result = LocaleAtlasBuilder.Build(new[] { ("m", (IModFileProvider)mod) });

            var onlyEn = result.Findings.Single(f => f.Key == "ui/only_en");
            ModAssert.Equal("key_missing_language", onlyEn.Code, "only_en code");
            ModAssert.Equal("ru", onlyEn.Language, "only_en lacks ru");
            ModAssert.True(onlyEn.Fix.StartsWith("locale/ui_ru.properties: ui/only_en = ", StringComparison.Ordinal), "fix goes to the sibling file: " + onlyEn.Fix);

            var onlyRu = result.Findings.Single(f => f.Key == "ui/only_ru");
            ModAssert.Equal("en", onlyRu.Language, "only_ru lacks en");
            ModAssert.Equal(4, result.InterestingKeys.Count(), "two keys + mod_m_name/_desc");

            var modName = result.Findings.Where(f => f.Key == "mod_m_name").ToList();
            ModAssert.Equal(2, modName.Count, "manifest name missing in both languages");
            ModAssert.True(modName.All(f => f.Code == "key_undeclared_optional" && f.Severity == LocaleSeverity.Warning), "optional: manifest text is the fallback");
        }

        [ModTest("map/mods_without_locale_skip_manifest_keys")]
        static void ModsWithoutLocaleSkipManifestKeys(ModTestContext ctx)
        {
            var localized = new MemoryProvider("a").Add("locale/en.properties", "mod_a_name = A\nmod_a_desc = A\n").Add("locale/ru.properties", "mod_a_name = А\nmod_a_desc = А\n");
            var bare = new MemoryProvider("b").Add("manifest.json", "{ \"id\": \"b\" }");
            var result = LocaleAtlasBuilder.Build(new[] { ("a", (IModFileProvider)localized), ("b", bare) });
            ModAssert.False(result.Keys.ContainsKey("mod_b_name"), "a mod without locale files is not asked for keys");
            ModAssert.Equal(0, result.Findings.Count, "clean");
        }

        [ModTest("map/undeclared_empty_and_duplicate")]
        static void UndeclaredEmptyAndDuplicate(ModTestContext ctx)
        {
            var mod = new MemoryProvider("m")
                .Add("locale/en.properties", "a/empty = \na/dup = One\n")
                .Add("locale/scenarios_en.properties", "a/dup = Two\n")
                .Add("locale/ru.properties", "a/empty = Пусто\na/dup = Один\n")
                .Add("scenarios/s.scenario", "text \"Привет\" key=a/missing\noption \"Hi \\\"there\\\"\" key=a/dup -> :x\n")
                .Add("src/Code.cs", "class C { string s = Tr(\"ui/from_code\"); // Tr(\"ui/commented\")\n}\n")
                .Add("items/i.json", "{ \"Name\": \"items/x/name\", \"Image\": \"items/x.png\" }");
            var result = LocaleAtlasBuilder.Build(new[] { ("m", (IModFileProvider)mod) });

            var missing = result.Findings.Where(f => f.Key == "a/missing").ToList();
            ModAssert.Equal(2, missing.Count, "undeclared in both languages");
            ModAssert.True(missing.All(f => f.Code == "key_undeclared"), "undeclared code");
            ModAssert.Equal("locale/scenarios_ru.properties: a/missing = Привет", missing.Single(f => f.Language == "ru").Fix, "ru fix reuses the Russian literal");
            ModAssert.Equal("locale/scenarios_en.properties: a/missing = …", missing.Single(f => f.Language == "en").Fix, "en fix needs a translation");

            ModAssert.True(result.Findings.Any(f => f.Key == "a/empty" && f.Code == "key_empty" && f.Language == "en"), "empty value");
            ModAssert.True(result.Findings.Any(f => f.Key == "a/dup" && f.Code == "key_conflict"), "conflicting duplicate");
            ModAssert.True(result.Keys.ContainsKey("ui/from_code"), "code reference");
            ModAssert.False(result.Keys.ContainsKey("ui/commented"), "commented code is ignored");
            ModAssert.True(result.Keys.ContainsKey("items/x/name"), "json text field");
            ModAssert.False(result.Keys.ContainsKey("items/x.png"), "paths are not keys");
            ModAssert.Equal("Hi \"there\"", result.Keys["a/dup"].References.Single().Fallback, "escaped literal");
        }

        [ModTest("map/partial_override_is_a_warning")]
        static void PartialOverrideIsWarning(ModTestContext ctx)
        {
            var baseMod = new MemoryProvider("base")
                .Add("locale/en.properties", "k = Base\n")
                .Add("locale/ru.properties", "k = База\n");
            var overrideMod = new MemoryProvider("over").Add("locale/en.properties", "k = Override\n");
            var result = LocaleAtlasBuilder.Build(new[] { ("base", (IModFileProvider)baseMod), ("over", overrideMod) });
            var finding = result.Findings.Single(f => f.Key == "k");
            ModAssert.Equal("key_partial_override", finding.Code, "code");
            ModAssert.True(finding.Severity == LocaleSeverity.Warning, "warning, ru still comes from base");
            ModAssert.Equal("over", finding.ModId, "owner");
        }

        [ModTest("map/collection_texts_need_every_language")]
        static void CollectionTextsNeedEveryLanguage(ModTestContext ctx)
        {
            var package = new MemoryProvider("c")
                .Add("locale/en.properties", "x = X\n")
                .Add("locale/ru.properties", "x = Икс\n")
                .Add("collection.json",
                    "{ \"id\": \"c\", \"name\": \"Game\", \"locale\": [ { \"lang\": \"ru\", \"name\": \"Игра\" } ],\n" +
                    "  \"mechanics\": [ { \"id\": \"m1\", \"name\": \"Story\" }, { \"id\": \"m2\", \"name\": \"@x\" } ] }");
            var result = LocaleAtlasBuilder.Build(new[] { (LocaleAtlasBuilder.CollectionSourcePrefix + "c", (IModFileProvider)package) });
            var missing = result.Findings.Where(f => f.Code == "collection_text_missing").ToList();
            ModAssert.Equal(1, missing.Count, "only the literal mechanic without ru: " + string.Join("; ", missing.Select(f => f.Message)));
            ModAssert.Equal("m1", missing[0].Key, "mechanic id");
            ModAssert.True(result.Keys["x"].References.Any(r => r.Via == "collection"), "@key is a reference");
        }

        /// <summary>In-memory mod: paths are mod-relative with '/' separators.</summary>
        private sealed class MemoryProvider : IModFileProvider
        {
            private readonly Dictionary<string, byte[]> files = new(StringComparer.Ordinal);

            public MemoryProvider(string mod) => Mod = mod;

            public string Mod { get; }

            public string RootPath => null;

            public MemoryProvider Add(string path, string text)
            {
                files[path] = Utf8.GetBytes(text);
                return this;
            }

            public bool FileExists(string path) => files.ContainsKey(path);

            public IEnumerable<string> ListFiles(string directory = "")
            {
                var prefix = string.IsNullOrEmpty(directory) ? string.Empty : directory.TrimEnd('/') + "/";
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var path in files.Keys.Where(p => p.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    var rest = path.Substring(prefix.Length);
                    var slash = rest.IndexOf('/');
                    var entry = slash < 0 ? path : prefix + rest.Substring(0, slash + 1);
                    if (seen.Add(entry))
                        yield return entry;
                }
            }

            public string ReadFileText(string path) => files.TryGetValue(path, out var bytes) ? Utf8.GetString(bytes) : null;

            public byte[] ReadFileBinary(string path) => files.TryGetValue(path, out var bytes) ? bytes : null;

            public Stream OpenStream(string path) => files.TryGetValue(path, out var bytes) ? new MemoryStream(bytes) : null;

            public string GetFileSystemPath(string path) => null;

            public string ResolveIncludePath(string includePath, string currentPath) => includePath;

            public void Dispose()
            {
            }
        }
    }
}
