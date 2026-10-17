using System;
using System.Collections.Generic;
using System.Linq;

namespace Cthangover.LocaleAtlas
{
    public enum LocaleSeverity
    {
        Info,
        Warning,
        Error,
    }

    /// <summary>One <c>key = value</c> line of a <c>locale/*.properties</c> file.</summary>
    public sealed class LocaleEntry
    {
        public string Key { get; set; }
        public string Language { get; set; }
        public string Value { get; set; }
        public string ModId { get; set; }
        public string File { get; set; }
        public int Line { get; set; }
    }

    /// <summary>
    /// A place that asks for a key: <c>scenario</c> (<c>key=</c>), <c>code</c>
    /// (<c>Translate</c>/<c>Tr</c>/<c>Locale =</c> literals), <c>json</c> (text
    /// fields of data), <c>scene</c> (auto-translated <c>.tscn</c> texts),
    /// <c>collection</c> (<c>"@key"</c>) or <c>manifest</c>
    /// (<c>mod_&lt;id&gt;_name/_desc</c>).
    /// </summary>
    public sealed class LocaleReference
    {
        public string Key { get; set; }
        public string ModId { get; set; }
        public string File { get; set; }
        public int Line { get; set; }
        public string Via { get; set; }

        /// <summary>Text shown when the key is absent (scenario literal); null when there is none.</summary>
        public string Fallback { get; set; }

        /// <summary>The runtime has a non-key fallback (manifest name/description), so a gap is a warning.</summary>
        public bool Optional { get; set; }
    }

    /// <summary>A finding of the pass; <see cref="Code"/> is stable for tools.</summary>
    public sealed class LocaleFinding
    {
        public LocaleSeverity Severity { get; set; }
        public string Code { get; set; }
        public string ModId { get; set; }
        public string Key { get; set; }
        public string Language { get; set; }
        public string File { get; set; }
        public int Line { get; set; }
        public string Message { get; set; }

        /// <summary>Where and what to write to fix it (e.g. <c>locale/ru.properties: key = …</c>); null when not applicable.</summary>
        public string Fix { get; set; }
    }

    /// <summary>
    /// Everything known about one key: declarations per language across all
    /// mods and every reference. A key is "complete" when each project
    /// language has a non-empty declaration in every mod that declares it.
    /// </summary>
    public sealed class LocaleKeyRow
    {
        public string Key { get; set; }

        public Dictionary<string, List<LocaleEntry>> Declarations { get; } = new(StringComparer.Ordinal);

        public List<LocaleReference> References { get; } = new();

        public List<string> MissingLanguages { get; } = new();

        public List<string> EmptyLanguages { get; } = new();

        /// <summary>Mod → languages it lacks while declaring the key in others (only for mods that declare the key).</summary>
        public Dictionary<string, List<string>> PartialMods { get; } = new(StringComparer.Ordinal);

        public bool Declared => Declarations.Count > 0;

        public bool Complete => Declared && MissingLanguages.Count == 0 && EmptyLanguages.Count == 0 && PartialMods.Count == 0;

        public IEnumerable<string> DeclaringMods => Declarations.Values.SelectMany(e => e).Select(e => e.ModId).Distinct();
    }

    /// <summary>Encoding facts of one locale file.</summary>
    public sealed class LocaleFileInfo
    {
        public string ModId { get; set; }
        public string File { get; set; }
        public string Language { get; set; }
        public int Bytes { get; set; }
        public int Keys { get; set; }

        /// <summary><c>UTF-8</c>, <c>UTF-8 BOM</c>, <c>UTF-16 LE/BE</c>, <c>UTF-32</c> or <c>invalid</c>.</summary>
        public string Encoding { get; set; }

        /// <summary><c>LF</c>, <c>CRLF</c>, <c>CR</c>, <c>mixed</c> or <c>none</c> (single line).</summary>
        public string LineEndings { get; set; }

        public int Issues { get; set; }
    }

    /// <summary>Result of one pass; report writers and the driver read only this.</summary>
    public sealed class LocaleAtlasResult
    {
        /// <summary>Project languages: every language that has at least one locale file in a scanned mod.</summary>
        public List<string> Languages { get; } = new();

        public List<string> Mods { get; } = new();

        public List<LocaleFileInfo> Files { get; } = new();

        public List<LocaleEntry> Entries { get; } = new();

        public List<LocaleReference> References { get; } = new();

        public SortedDictionary<string, LocaleKeyRow> Keys { get; } = new(StringComparer.Ordinal);

        public List<LocaleFinding> Findings { get; } = new();

        public IEnumerable<LocaleFinding> Errors => Findings.Where(f => f.Severity == LocaleSeverity.Error);

        public IEnumerable<LocaleFinding> Warnings => Findings.Where(f => f.Severity == LocaleSeverity.Warning);

        /// <summary>Keys worth looking at: everything that is not complete in every language.</summary>
        public IEnumerable<LocaleKeyRow> InterestingKeys => Keys.Values.Where(k => !k.Complete);

        /// <summary>Mods with at least one error, in scan order.</summary>
        public List<string> FailingMods()
        {
            var failing = new HashSet<string>(Errors.Select(f => f.ModId), StringComparer.Ordinal);
            return Mods.Where(failing.Contains).ToList();
        }
    }
}
