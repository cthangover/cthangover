using System;
using System.Collections.Generic;

namespace Mods.ModManager
{
    /// <summary>
    /// Presentation helpers for catalog provenance. The manager shows where a mod
    /// comes from without cluttering the tile: a short host label plus the full
    /// URL list in the tooltip, and a "+N" marker when several catalogs publish
    /// the same build.
    /// </summary>
    public static class ModCatalogSource
    {
        /// <summary>First source URL, or null when there are none.</summary>
        public static string FirstUrl(IReadOnlyList<string> urls)
            => urls != null && urls.Count > 0 ? urls[0] : null;

        /// <summary>Host of a URL (fallback: the raw string when it does not parse).</summary>
        public static string Host(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
                return uri.Host;

            return url;
        }

        /// <summary>Short label: host plus "+N" for extra catalogs, or null when unknown.</summary>
        public static string Label(IReadOnlyList<string> urls)
        {
            var first = FirstUrl(urls);
            if (first == null)
                return null;

            var host = Host(first);
            var extra = urls.Count - 1;
            return extra > 0 ? $"{host} +{extra}" : host;
        }

        /// <summary>Newline-joined full URL list for tooltips.</summary>
        public static string Tooltip(IReadOnlyList<string> urls)
            => urls == null || urls.Count == 0 ? null : string.Join("\n", urls);
    }
}
