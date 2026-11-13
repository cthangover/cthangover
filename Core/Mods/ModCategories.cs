using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Cthangover.Core.Mods;

/// <summary>Categories are author-written strings. Only surrounding whitespace, empty names and case-insensitive duplicates are normalized.</summary>
public static class ModCategories
{
    public const string Story = "story", Characters = "characters", Gameplay = "gameplay", Visual = "visual",
        Audio = "audio", UI = "ui", Locale = "locale", Library = "library", Engine = "engine",
        Overhaul = "overhaul", Tools = "tools", Editor = "editor", Debug = "debug", Other = "other";

    /// <summary>The same identity comparison is used for membership, grouping, counts and completion.</summary>
    public static StringComparer Comparer { get; } = StringComparer.OrdinalIgnoreCase;

    /// <summary>Ready-translated suggestions, not a whitelist or a filter order.</summary>
    public static IReadOnlyList<string> Base { get; } = Array.AsReadOnly(new[]
        { Story, Characters, Gameplay, Visual, Audio, UI, Locale, Library, Engine, Overhaul, Tools, Editor, Debug, Other });

    /// <summary>Preserves the spelling and order of the first occurrence. There is no client category count or length limit.</summary>
    public static List<string> Normalize(IEnumerable<string> categories)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(Comparer);
        foreach (var value in categories ?? Array.Empty<string>())
        {
            var category = value?.Trim();
            if (!string.IsNullOrEmpty(category) && seen.Add(category)) result.Add(category);
        }
        return result;
    }

    /// <summary>An omitted/empty set belongs to Other without changing the stored list.</summary>
    public static string Primary(IEnumerable<string> categories) => Normalize(categories).FirstOrDefault() ?? Other;

    /// <summary>Effective membership for filters and counters, including the implicit Other bucket.</summary>
    public static List<string> Effective(IEnumerable<string> categories)
    {
        var normalized = Normalize(categories);
        if (normalized.Count == 0) normalized.Add(Other);
        return normalized;
    }

    /// <summary>JSON shape is a manifest/record contract. All strings are accepted; non-string values are JSON type errors.</summary>
    public static List<string> Read(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw new JsonException("categories must be a JSON array of strings");
        var categories = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw new JsonException("categories must be a JSON array of strings; a non-string element was found");
            categories.Add(item.GetString());
        }
        return Normalize(categories);
    }
}
