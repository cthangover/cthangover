using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Cthangover.Core.Mods
{
    /// <summary>
    /// One mechanic inside a <see cref="ModCollection"/> — a named role that a
    /// group of mods plays in the collection ("card battle", "novel interface",
    /// "developer tools"). Mechanics are declared by the collection, not by the
    /// mods: the same mod may be the backbone of one collection and an optional
    /// layer of another, so the role cannot live in the mod manifest.
    /// <para/>
    /// <see cref="Mods"/> entries use the same versioned string format as
    /// <c>manifest.json</c> dependencies (<c>"turn_battle_base&gt;=1"</c>). The
    /// optional suffix <c>"?"</c> is accepted but ignored — optionality of a
    /// whole mechanic is expressed by <see cref="Optional"/>.
    /// </summary>
    public class ModCollectionMechanic
    {
        /// <summary>Stable id unique within the collection; used by the graph view to group nodes.</summary>
        [JsonPropertyName("id")]
        public string Id { get; set; }

        /// <summary>Human-readable mechanic name shown in the graph frame header.</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; }

        /// <summary>Short explanation of what the mechanic adds to the game.</summary>
        [JsonPropertyName("description")]
        public string Description { get; set; }

        /// <summary>
        /// Slot role of the mechanic ("characters", "story", "battle", ...).
        /// The scheme editor groups the mechanic's mods under this label and the
        /// resolver uses it to detect two required blocks claiming one slot
        /// together with <see cref="ModCollectionInclude.Role"/>.
        /// </summary>
        [JsonPropertyName("role")]
        public string Role { get; set; }

        /// <summary>
        /// Versioned mod references of this mechanic, same syntax as
        /// <c>manifest.json</c> <c>depends</c> (e.g. <c>"interface&gt;=1"</c>).
        /// </summary>
        [JsonPropertyName("mods")]
        public List<string> Mods { get; set; }

        /// <summary>
        /// True when the mechanic can be skipped without breaking the rest of the
        /// collection — the seed of the future game constructor. The resolver
        /// still includes optional mechanics in the graph and the install plan.
        /// </summary>
        [JsonPropertyName("optional")]
        public bool Optional { get; set; }

        /// <summary>
        /// Player's choice for an optional mechanic. <c>null</c> (omitted) means
        /// the default: the mechanic is on until the player turns it off.
        /// </summary>
        [JsonPropertyName("selected")]
        public bool? Selected { get; set; }

        /// <summary>True when the mechanic participates in resolution (not an off optional).</summary>
        [JsonIgnore]
        public bool IsSelected => !(Optional && Selected == false);

        /// <summary>
        /// Replaces this group's explicit requirement for a mod, leaving other
        /// groups untouched. Rejects malformed operators/versions before any
        /// mutation; an empty operator removes the pin. Repeated references in
        /// the same group receive the same rule, preserving each optional suffix.
        /// </summary>
        public bool TrySetModVersion(string modId, string versionOperator, string version)
        {
            if (string.IsNullOrWhiteSpace(modId) || Mods == null)
                return false;

            var op = versionOperator?.Trim() ?? string.Empty;
            if (op != string.Empty && op != ">=" && op != "<=" && op != ">"
                && op != "<" && op != "=" && op != "^")
                return false;

            var value = version?.Trim() ?? string.Empty;
            if (op.Length > 0 && value.Length == 0)
                return false;

            var candidate = ModDependency.Parse(modId + op + (op.Length > 0 ? value : string.Empty));
            if (!candidate.IsValid || candidate.IsOptional
                || !string.Equals(candidate.ModId, modId, System.StringComparison.OrdinalIgnoreCase)
                || (op.Length > 0 && candidate.VersionRaw != value))
                return false;

            var changed = false;
            for (var i = 0; i < Mods.Count; i++)
            {
                var current = ModDependency.Parse(Mods[i]);
                if (!current.IsValid || !string.Equals(current.ModId, modId, System.StringComparison.OrdinalIgnoreCase))
                    continue;

                Mods[i] = candidate.ToString() + (current.IsOptional ? "?" : string.Empty);
                changed = true;
            }
            return changed;
        }

        /// <summary>
        /// Localized name/description entries, same shape as catalog mod
        /// entries; the scheme editor shows the match for the player's language
        /// and falls back to <see cref="Name"/> and <see cref="Description"/>.
        /// </summary>
        [JsonPropertyName("locale")]
        public List<CatalogLocaleEntry> Locale { get; set; }

        /// <summary>
        /// Name for the given language: requested locale entry -> flat name ->
        /// English locale entry -> first entry -> id. A flat '@key' value is
        /// returned verbatim; the caller (UI or
        /// <see cref="ModCollectionResolver"/>) resolves it through
        /// <see cref="CollectionText.Resolve"/>, keeping this method free of
        /// Godot calls.
        /// </summary>
        public string GetLocalizedName(string language)
        {
            var localized = Match(language, true, englishFallback: false);
            if (localized != null)
                return localized;

            if (!string.IsNullOrEmpty(Name))
                return Name;

            localized = Match(language, true, englishFallback: true);
            if (localized != null)
                return localized;

            var first = Locale?.FirstOrDefault(l => !string.IsNullOrEmpty(l.Name));
            return first?.Name ?? Id;
        }

        /// <summary>
        /// Description for the given language: requested locale entry -> flat
        /// description -> English locale entry -> first entry. A flat '@key'
        /// value is returned verbatim for the caller's translator to resolve.
        /// </summary>
        public string GetLocalizedDescription(string language)
        {
            var localized = Match(language, false, englishFallback: false);
            if (localized != null)
                return localized;

            if (!string.IsNullOrEmpty(Description))
                return Description;

            localized = Match(language, false, englishFallback: true);
            if (localized != null)
                return localized;

            var first = Locale?.FirstOrDefault(l => !string.IsNullOrEmpty(l.Description));
            return first?.Description ?? string.Empty;
        }

        /// <summary>
        /// Scans <see cref="Locale"/> for the requested language; with
        /// <paramref name="englishFallback"/> the 'en' entry is consulted as a
        /// second pass, so callers can slot the flat fields between the two.
        /// </summary>
        private string Match(string language, bool wantName, bool englishFallback)
        {
            if (Locale == null || Locale.Count == 0 || string.IsNullOrEmpty(language))
                return null;

            foreach (var entry in Locale)
            {
                if (string.Equals(entry.Lang, language, System.StringComparison.OrdinalIgnoreCase) && HasText(entry, wantName))
                    return wantName ? entry.Name : entry.Description;
            }

            if (!englishFallback)
                return null;

            foreach (var entry in Locale)
            {
                if (string.Equals(entry.Lang, "en", System.StringComparison.OrdinalIgnoreCase) && HasText(entry, wantName))
                    return wantName ? entry.Name : entry.Description;
            }

            return null;
        }

        private static bool HasText(CatalogLocaleEntry entry, bool wantName)
            => wantName ? !string.IsNullOrEmpty(entry.Name) : !string.IsNullOrEmpty(entry.Description);
    }
}
