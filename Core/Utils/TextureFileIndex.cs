using System;
using System.Collections.Generic;
using System.IO;

namespace Cthangover.Core.Utils
{
    /// <summary>
    /// Indexes texture keys in their original order. Exact spelling is case-sensitive;
    /// dot-prefix aliases are case-insensitive, and an earlier alias beats a later exact key.
    /// </summary>
    public sealed class TextureFileIndex
    {
        private readonly List<string> keys = new();
        private readonly Dictionary<string, int> exact = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> prefixes = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Builds one immutable index for a collected file-list snapshot and its permitted extensions.</summary>
        public TextureFileIndex(IEnumerable<string> fileKeys, ISet<string> extensions)
        {
            foreach (var key in fileKeys)
            {
                if (!extensions.Contains(Path.GetExtension(key))) continue;
                var index = keys.Count;
                keys.Add(key);
                exact.TryAdd(key, index);
                for (var dot = key.IndexOf('.'); dot >= 0; dot = key.IndexOf('.', dot + 1))
                    prefixes.TryAdd(key.Substring(0, dot), index);
            }
        }

        /// <summary>Returns the first permitted key matching the exact id or id followed by a dot; null on a miss.</summary>
        public string Find(string id)
        {
            if (id == null) return null;
            var index = exact.TryGetValue(id, out var literal) ? literal : int.MaxValue;
            if (prefixes.TryGetValue(id, out var prefix)) index = Math.Min(index, prefix);
            return index == int.MaxValue ? null : keys[index];
        }
    }
}
