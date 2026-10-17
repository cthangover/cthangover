using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Cthangover.LocaleAtlas
{
    /// <summary>
    /// Writes <c>report.md</c> (read first: errors with the fix, the map of
    /// keys that are not complete in every language, file encodings) and
    /// <c>report.json</c> (the same for tools). Keys declared non-empty in
    /// every language are counted but not listed — they are not interesting.
    /// </summary>
    public static class LocaleAtlasReportWriter
    {
        public static void Write(LocaleAtlasResult result, string directory)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "report.md"), BuildMarkdown(result), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(directory, "report.json"), BuildJson(result), new UTF8Encoding(false));
        }

        public static string BuildMarkdown(LocaleAtlasResult result)
        {
            var md = new StringBuilder();
            var errors = result.Errors.ToList();
            var warnings = result.Warnings.ToList();
            var interesting = result.InterestingKeys.ToList();

            md.AppendLine("# Карта ключей локализации");
            md.AppendLine();
            md.AppendLine($"Создано: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC · модов: {result.Mods.Count} · файлов локализации: {result.Files.Count} · " +
                          $"языки: {string.Join(", ", result.Languages.Select(l => $"`{l}`"))}");
            md.AppendLine();
            md.AppendLine($"Ключей: {result.Keys.Count} · во всех языках: {result.Keys.Count - interesting.Count} · " +
                          $"**требуют внимания: {interesting.Count}** · ссылок на ключи: {result.References.Count}");
            md.AppendLine();
            md.AppendLine($"**Ошибок: {errors.Count}** (моды: {string.Join(", ", result.FailingMods()).OrEmpty("нет")}) · предупреждений: {warnings.Count}");
            md.AppendLine();
            md.AppendLine("Как читать: ключ попадает в отчёт, только если он не объявлен непустым во всех языках " +
                          "каждого мода, который его объявляет, или на него ссылаются, но он нигде не объявлен. " +
                          "`✓` — объявлен, `∅` — пустое значение, `—` — нет в этом языке. Колонка «Исправление» — файл и строка, которые нужно добавить.");
            md.AppendLine();

            AppendEncoding(md, result);
            AppendKeyMap(md, result, interesting);
            AppendFindings(md, "Необъявленные ключи (есть ссылка, нет ни в одной локализации)", result.Findings.Where(f => f.Code is "key_undeclared" or "key_undeclared_optional"));
            AppendFindings(md, "Ключ есть не во всех языках", result.Findings.Where(f => f.Code is "key_missing_language" or "key_partial_override"));
            AppendFindings(md, "Пустые значения", result.Findings.Where(f => f.Code == "key_empty"));
            AppendFindings(md, "Повторные объявления в одном моде", result.Findings.Where(f => f.Code is "key_duplicate" or "key_conflict"));
            AppendFindings(md, "Тексты коллекций без перевода", result.Findings.Where(f => f.Code == "collection_text_missing"));
            AppendFindings(md, "Прочее (плейсхолдеры, непереведённый текст, формат строк)",
                result.Findings.Where(f => f.Code is "placeholder_mismatch" or "untranslated" or "line_without_separator" or "empty_key" or "key_whitespace" or "file_not_loaded"));
            AppendModTable(md, result);
            return md.ToString();
        }

        private static void AppendEncoding(StringBuilder md, LocaleAtlasResult result)
        {
            md.AppendLine("## Кодировки файлов локализации");
            md.AppendLine();
            var encodingCodes = new HashSet<string>(StringComparer.Ordinal)
            {
                "encoding_utf32", "encoding_utf16", "encoding_utf16_nobom", "utf8_bom", "invalid_utf8", "mixed_line_endings", "cr_line_endings",
                "replacement_char", "control_char", "inner_bom", "mojibake_cp1251", "mojibake_latin1", "question_marks",
            };
            var issues = result.Findings.Where(f => encodingCodes.Contains(f.Code)).ToList();
            var byEncoding = result.Files.GroupBy(f => f.Encoding).Select(g => $"{g.Key}: {g.Count()}");
            var byEndings = result.Files.GroupBy(f => f.LineEndings).Select(g => $"{g.Key}: {g.Count()}");
            md.AppendLine($"Кодировка — {string.Join(", ", byEncoding)}; переводы строк — {string.Join(", ", byEndings)}.");
            md.AppendLine();
            if (issues.Count == 0)
            {
                md.AppendLine("Проблем нет: все файлы — корректный UTF-8, без U+FFFD, управляющих символов и признаков кракозябр (cp1251/latin-1).");
                md.AppendLine();
            }
            else
            {
                md.AppendLine("| Серьёзность | Мод | Файл | Строка | Проблема |");
                md.AppendLine("|---|---|---|---|---|");
                foreach (var f in issues.OrderByDescending(f => f.Severity).ThenBy(f => f.ModId, StringComparer.Ordinal).ThenBy(f => f.File, StringComparer.Ordinal).ThenBy(f => f.Line))
                    md.AppendLine($"| {Severity(f.Severity)} | `{f.ModId}` | `{f.File}` | {(f.Line > 0 ? f.Line.ToString() : "—")} | {Cell(f.Message)} |");
                md.AppendLine();
            }

            md.AppendLine("<details><summary>Все файлы</summary>");
            md.AppendLine();
            md.AppendLine("| Мод | Файл | Язык | Кодировка | Переводы строк | Ключей | Байт |");
            md.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var f in result.Files)
                md.AppendLine($"| `{f.ModId}` | `{f.File}` | {f.Language} | {f.Encoding} | {f.LineEndings} | {f.Keys} | {f.Bytes} |");
            md.AppendLine();
            md.AppendLine("</details>");
            md.AppendLine();
        }

        private static void AppendKeyMap(StringBuilder md, LocaleAtlasResult result, List<LocaleKeyRow> interesting)
        {
            md.AppendLine("## Карта ключей, требующих внимания");
            md.AppendLine();
            if (interesting.Count == 0)
            {
                md.AppendLine("Нет: каждый ключ объявлен непустым во всех языках, необъявленных ссылок нет.");
                md.AppendLine();
                return;
            }

            foreach (var group in interesting.GroupBy(Owner).OrderBy(g => result.Mods.IndexOf(g.Key)))
            {
                md.AppendLine($"### `{group.Key}`");
                md.AppendLine();
                md.AppendLine("| Ключ | " + string.Join(" | ", result.Languages) + " | Объявлен в | Ссылки | Проблема |");
                md.AppendLine("|---|" + string.Concat(result.Languages.Select(_ => "---|")) + "---|---|---|");
                foreach (var row in group)
                {
                    var cells = result.Languages.Select(l => !row.Declarations.TryGetValue(l, out var e) ? "—" : e.Any(x => x.Value.Length == 0) ? "∅" : "✓");
                    var declared = row.Declared
                        ? string.Join(", ", row.Declarations.Values.SelectMany(e => e).Select(e => $"{e.ModId}:{Path.GetFileName(e.File)}").Distinct())
                        : "нигде";
                    var refs = row.References.Count == 0
                        ? "—"
                        : string.Join(", ", row.References.GroupBy(r => (r.ModId, r.Via)).Select(g => $"{g.Key.ModId} ({g.Key.Via}{(g.Count() > 1 ? $" ×{g.Count()}" : string.Empty)})"));
                    md.AppendLine($"| `{row.Key}` | {string.Join(" | ", cells)} | {Cell(declared)} | {Cell(refs)} | {Problem(row)} |");
                }

                md.AppendLine();
            }
        }

        private static void AppendFindings(StringBuilder md, string title, IEnumerable<LocaleFinding> findings)
        {
            var list = findings.ToList();
            md.AppendLine($"## {title}");
            md.AppendLine();
            if (list.Count == 0)
            {
                md.AppendLine("Нет.");
                md.AppendLine();
                return;
            }

            md.AppendLine("| Серьёзность | Мод | Ключ | Язык | Где | Описание | Исправление |");
            md.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var f in list.OrderByDescending(f => f.Severity).ThenBy(f => f.ModId, StringComparer.Ordinal).ThenBy(f => f.Key, StringComparer.Ordinal).ThenBy(f => f.Language, StringComparer.Ordinal))
            {
                md.AppendLine($"| {Severity(f.Severity)} | `{f.ModId}` | `{f.Key}` | {f.Language} | `{f.File}{(f.Line > 0 ? ":" + f.Line : string.Empty)}` | " +
                              $"{Cell(f.Message)} | {(f.Fix == null ? "—" : $"`{Cell(f.Fix)}`")} |");
            }

            md.AppendLine();
        }

        private static void AppendModTable(StringBuilder md, LocaleAtlasResult result)
        {
            md.AppendLine("## Сводка по модам");
            md.AppendLine();
            md.AppendLine("| Мод | Файлов | Ключей | Требуют внимания | Ошибок | Предупреждений |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (var mod in result.Mods)
            {
                var files = result.Files.Count(f => f.ModId == mod);
                var keys = result.Entries.Where(e => e.ModId == mod).Select(e => e.Key).Distinct().Count();
                var attention = result.InterestingKeys.Count(r => Owner(r) == mod);
                var errors = result.Errors.Count(f => f.ModId == mod);
                var warnings = result.Warnings.Count(f => f.ModId == mod);
                if (files == 0 && attention == 0 && errors == 0 && warnings == 0)
                    continue;
                md.AppendLine($"| `{mod}` | {files} | {keys} | {attention} | {errors} | {warnings} |");
            }

            md.AppendLine();
        }

        /// <summary>The mod that should fix the key: the first declaring mod, else the first referencing one.</summary>
        private static string Owner(LocaleKeyRow row)
            => row.PartialMods.Keys.FirstOrDefault()
               ?? row.Declarations.Values.SelectMany(e => e).Select(e => e.ModId).FirstOrDefault()
               ?? row.References.Select(r => r.ModId).FirstOrDefault()
               ?? "?";

        private static string Problem(LocaleKeyRow row)
        {
            var parts = new List<string>();
            if (!row.Declared)
                parts.Add(row.References.All(r => r.Optional) ? "не объявлен (есть запасной текст манифеста)" : "не объявлен");
            if (row.MissingLanguages.Count > 0)
                parts.Add("нет в " + string.Join(", ", row.MissingLanguages));
            if (row.EmptyLanguages.Count > 0)
                parts.Add("пусто в " + string.Join(", ", row.EmptyLanguages));
            foreach (var (mod, lacks) in row.PartialMods)
            {
                // Languages missing everywhere are already listed; here only overrides that cover part of the languages.
                var own = lacks.Except(row.MissingLanguages).ToList();
                if (own.Count > 0)
                    parts.Add($"{mod} переопределяет без {string.Join(", ", own)}");
            }

            return string.Join("; ", parts);
        }

        public static string BuildJson(LocaleAtlasResult result)
        {
            var payload = new
            {
                generated = DateTime.UtcNow.ToString("o"),
                languages = result.Languages,
                mods = result.Mods,
                summary = new
                {
                    files = result.Files.Count,
                    keys = result.Keys.Count,
                    complete = result.Keys.Values.Count(k => k.Complete),
                    attention = result.InterestingKeys.Count(),
                    references = result.References.Count,
                    errors = result.Errors.Count(),
                    warnings = result.Warnings.Count(),
                    failingMods = result.FailingMods(),
                },
                files = result.Files.Select(f => new { mod = f.ModId, file = f.File, language = f.Language, encoding = f.Encoding, lineEndings = f.LineEndings, keys = f.Keys, bytes = f.Bytes }),
                keys = result.InterestingKeys.Select(r => new
                {
                    key = r.Key,
                    declared = r.Declared,
                    missing = r.MissingLanguages,
                    empty = r.EmptyLanguages,
                    partial = r.PartialMods,
                    declarations = r.Declarations.ToDictionary(
                        d => d.Key,
                        d => d.Value.Select(e => new { mod = e.ModId, file = e.File, line = e.Line, value = e.Value })),
                    references = r.References.Select(x => new { mod = x.ModId, file = x.File, line = x.Line, via = x.Via, fallback = x.Fallback, optional = x.Optional }),
                }),
                findings = result.Findings.Select(f => new
                {
                    severity = f.Severity.ToString().ToLowerInvariant(), code = f.Code, mod = f.ModId, key = f.Key, language = f.Language,
                    file = f.File, line = f.Line, message = f.Message, fix = f.Fix,
                }),
            };

            return JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
        }

        private static string Severity(LocaleSeverity severity) => severity switch
        {
            LocaleSeverity.Error => "ошибка",
            LocaleSeverity.Warning => "предупреждение",
            _ => "инфо",
        };

        private static string Cell(string text) => (text ?? string.Empty).Replace("|", "\\|").Replace("\n", " ");
    }
}
