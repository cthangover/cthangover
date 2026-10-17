using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Cthangover.ResourceAtlas
{
    /// <summary>
    /// Writes <c>report.md</c> (read first: errors with the fix, then the
    /// mod-to-mod picture) and <c>report.json</c> (every finding, for tools).
    /// Repeated occurrences of one link are folded into a count with the first
    /// location, so the markdown stays readable for agents and people.
    /// </summary>
    public static class ResourceAtlasReportWriter
    {
        private const int Samples = 5;

        public static void Write(ResourceAtlasResult result, string directory)
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "report.md"), BuildMarkdown(result), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(directory, "report.json"), BuildJson(result), new UTF8Encoding(false));
        }

        public static string BuildMarkdown(ResourceAtlasResult result)
        {
            var md = new StringBuilder();
            var errors = Fold(result.Errors).ToList();
            var warnings = result.Warnings.ToList();

            md.AppendLine("# Карта ресурсов модов");
            md.AppendLine();
            md.AppendLine($"Создано: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC · модов: {result.LoadOrder.Count} · " +
                          $"ресурсов: {result.Provisions.Count} · запросов: {result.Requests.Count} · патчей: {result.Patches.Count}");
            md.AppendLine();
            md.AppendLine($"**Ошибок: {errors.Count}** (моды: {string.Join(", ", result.FailingMods()).OrEmpty("нет")}) · " +
                          $"предупреждений: {warnings.Count}");
            md.AppendLine();
            md.AppendLine("Как читать: связь «A → B» значит, что мод A берёт ресурс мода B по ключу. Связь корректна, " +
                          "когда B есть в транзитивных `depends` мода A. При совпадении ключа выигрывает мод, загруженный позже, " +
                          "и это детерминировано только вдоль цепочки `depends`.");
            md.AppendLine();

            md.AppendLine("## Ошибки");
            md.AppendLine();
            if (errors.Count == 0)
                md.AppendLine("Нет.");
            foreach (var group in errors.GroupBy(e => e.First.Kind))
            {
                md.AppendLine($"### {Title(group.Key)}");
                md.AppendLine();
                foreach (var error in group)
                {
                    var f = error.First;
                    md.AppendLine($"- `{f.ModId}` → `{string.Join("`, `", f.OtherMods)}`: {f.ResourceKind} `{f.ResourceId}` — {f.Message}. " +
                                  $"{Location(f)}{(error.Count > 1 ? $" (+{error.Count - 1})" : string.Empty)}");
                }
                md.AppendLine();
            }

            AppendLinks(md, result);
            AppendOverrides(md, result);
            AppendPatches(md, result);
            AppendUnresolved(md, warnings);
            AppendModTable(md, result);

            md.AppendLine("## Порядок загрузки");
            md.AppendLine();
            md.AppendLine(string.Join(" → ", result.LoadOrder.Select(m => $"`{m}`")));
            return md.ToString();
        }

        private static void AppendLinks(StringBuilder md, ResourceAtlasResult result)
        {
            md.AppendLine("## Связи между модами (кто чьи ресурсы использует)");
            md.AppendLine();
            var links = result.Findings
                .Where(f => f.Kind is AtlasFindingKind.DeclaredUse or AtlasFindingKind.UndeclaredUse)
                .SelectMany(f => f.OtherMods.Select(other => (f, other)))
                .GroupBy(x => (x.f.ModId, x.other))
                .OrderBy(g => g.Key.ModId, StringComparer.Ordinal).ThenBy(g => g.Key.other, StringComparer.Ordinal)
                .ToList();
            if (links.Count == 0)
            {
                md.AppendLine("Нет.");
                md.AppendLine();
                return;
            }

            md.AppendLine("| Мод | Берёт ресурсы у | В depends | Запросов | Виды | Примеры |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (var link in links)
            {
                var declared = result.Closures[link.Key.ModId].Contains(link.Key.other);
                var kinds = string.Join(", ", link.Select(x => x.f.ResourceKind).Distinct());
                var samples = string.Join(", ", link.Select(x => $"`{x.f.ResourceId}`").Distinct().Take(Samples));
                md.AppendLine($"| `{link.Key.ModId}` | `{link.Key.other}` | {(declared ? "да" : "**нет**")} | {link.Count()} | {kinds} | {samples} |");
            }
            md.AppendLine();
        }

        private static void AppendOverrides(StringBuilder md, ResourceAtlasResult result)
        {
            md.AppendLine("## Переопределения (один ключ у нескольких модов)");
            md.AppendLine();
            var overrides = result.Findings
                .Where(f => f.Kind is AtlasFindingKind.DeclaredOverride or AtlasFindingKind.UnorderedOverride)
                .GroupBy(f => (f.ModId, Other: f.OtherMods[0], f.Kind))
                .OrderBy(g => g.Key.Kind).ThenBy(g => g.Key.ModId, StringComparer.Ordinal)
                .ToList();
            if (overrides.Count == 0)
            {
                md.AppendLine("Нет.");
                md.AppendLine();
                return;
            }

            md.AppendLine("| Побеждает | Перекрывает | Упорядочено depends | Ключей | Виды | Примеры |");
            md.AppendLine("|---|---|---|---|---|---|");
            foreach (var group in overrides)
            {
                var kinds = string.Join(", ", group.Select(f => f.ResourceKind).Distinct());
                var samples = string.Join(", ", group.Select(f => $"`{f.ResourceId}`").Distinct().Take(Samples));
                var ordered = group.Key.Kind == AtlasFindingKind.DeclaredOverride ? "да" : "**нет**";
                md.AppendLine($"| `{group.Key.ModId}` | `{group.Key.Other}` | {ordered} | {group.Count()} | {kinds} | {samples} |");
            }
            md.AppendLine();
        }

        private static void AppendPatches(StringBuilder md, ResourceAtlasResult result)
        {
            md.AppendLine("## Патчи чужих ресурсов");
            md.AppendLine();
            var patches = result.Findings
                .Where(f => f.Kind is AtlasFindingKind.DeclaredPatch or AtlasFindingKind.UndeclaredPatch)
                .ToList();
            if (patches.Count == 0)
            {
                md.AppendLine("Нет.");
                md.AppendLine();
                return;
            }

            md.AppendLine("| Мод | Патчит мод | В depends | Ресурс | Файл |");
            md.AppendLine("|---|---|---|---|---|");
            foreach (var patch in patches.OrderBy(p => p.ModId, StringComparer.Ordinal))
            {
                var declared = patch.Kind == AtlasFindingKind.DeclaredPatch ? "да" : "**нет**";
                md.AppendLine($"| `{patch.ModId}` | `{string.Join("`, `", patch.OtherMods)}` | {declared} | {patch.ResourceKind} `{patch.ResourceId}` | `{patch.File}` |");
            }
            md.AppendLine();
        }

        private static void AppendUnresolved(StringBuilder md, List<AtlasFinding> warnings)
        {
            md.AppendLine("## Предупреждения: ключ не найден ни в одном моде");
            md.AppendLine();
            if (warnings.Count == 0)
            {
                md.AppendLine("Нет.");
                md.AppendLine();
                return;
            }

            foreach (var group in Fold(warnings).OrderBy(w => w.First.ModId, StringComparer.Ordinal))
            {
                var f = group.First;
                md.AppendLine($"- `{f.ModId}`: {f.ResourceKind} `{f.ResourceId}` — {f.Message}. {Location(f)}{(group.Count > 1 ? $" (+{group.Count - 1})" : string.Empty)}");
            }
            md.AppendLine();
        }

        private static void AppendModTable(StringBuilder md, ResourceAtlasResult result)
        {
            md.AppendLine("## Сводка по модам");
            md.AppendLine();
            md.AppendLine("| Мод | depends | Берёт у | Его ресурсы берут | Перекрывает | Ошибок |");
            md.AppendLine("|---|---|---|---|---|---|");
            var uses = result.Findings.Where(f => f.Kind is AtlasFindingKind.DeclaredUse or AtlasFindingKind.UndeclaredUse).ToList();
            var overrides = result.Findings.Where(f => f.Kind is AtlasFindingKind.DeclaredOverride or AtlasFindingKind.UnorderedOverride).ToList();
            foreach (var mod in result.LoadOrder)
            {
                var takesFrom = uses.Where(f => f.ModId == mod).SelectMany(f => f.OtherMods).Distinct().OrderBy(m => m, StringComparer.Ordinal);
                var usedBy = uses.Where(f => f.OtherMods.Contains(mod)).Select(f => f.ModId).Distinct().OrderBy(m => m, StringComparer.Ordinal);
                var covers = overrides.Where(f => f.ModId == mod).SelectMany(f => f.OtherMods).Distinct().OrderBy(m => m, StringComparer.Ordinal);
                var errors = result.Errors.Count(f => f.ModId == mod);
                md.AppendLine($"| `{mod}` | {Join(result.Depends[mod])} | {Join(takesFrom)} | {Join(usedBy)} | {Join(covers)} | {(errors > 0 ? $"**{errors}**" : "0")} |");
            }
            md.AppendLine();
        }

        public static string BuildJson(ResourceAtlasResult result)
        {
            var report = new
            {
                generatedAt = DateTime.UtcNow.ToString("O"),
                summary = new
                {
                    mods = result.LoadOrder.Count,
                    provisions = result.Provisions.Count,
                    requests = result.Requests.Count,
                    patches = result.Patches.Count,
                    errors = result.Errors.Count(),
                    warnings = result.Warnings.Count(),
                    failingMods = result.FailingMods(),
                },
                loadOrder = result.LoadOrder,
                mods = result.LoadOrder.Select(m => new { id = m, depends = result.Depends[m], closure = result.Closures[m].OrderBy(x => x, StringComparer.Ordinal) }),
                findings = result.Findings.Select(f => new
                {
                    severity = f.Severity.ToString().ToLowerInvariant(),
                    kind = f.Kind.ToString(),
                    mod = f.ModId,
                    otherMods = f.OtherMods,
                    resourceKind = f.ResourceKind,
                    resourceId = f.ResourceId,
                    file = f.File,
                    line = f.Line,
                    message = f.Message,
                }),
            };

            return JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
        }

        /// <summary>Groups repeated occurrences of one logical finding (same mods, kind and key).</summary>
        private static IEnumerable<(AtlasFinding First, int Count)> Fold(IEnumerable<AtlasFinding> findings)
            => findings
                .GroupBy(f => (f.Kind, f.ModId, Others: string.Join(",", f.OtherMods), f.ResourceKind, f.ResourceId))
                .Select(g => (g.First(), g.Count()));

        private static string Title(AtlasFindingKind kind) => kind switch
        {
            AtlasFindingKind.UndeclaredUse => "Используется чужой ресурс без зависимости в depends",
            AtlasFindingKind.UnorderedOverride => "Переопределение без порядка (победитель случаен)",
            AtlasFindingKind.UndeclaredPatch => "Патч чужого ресурса без зависимости",
            AtlasFindingKind.OutsideGame => "Ресурс есть только в модах вне игры",
            _ => kind.ToString(),
        };

        private static string Location(AtlasFinding finding)
            => string.IsNullOrEmpty(finding.File)
                ? string.Empty
                : finding.Line > 0 ? $"`{finding.File}:{finding.Line}`" : $"`{finding.File}`";

        private static string Join(IEnumerable<string> ids)
        {
            var list = ids.ToList();
            return list.Count == 0 ? "—" : string.Join(", ", list);
        }

        private static string OrEmpty(this string value, string fallback)
            => string.IsNullOrEmpty(value) ? fallback : value;
    }
}
