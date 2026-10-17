namespace Cthangover.Tests.Mods.Unload
{
    using System.Collections.Generic;

    /// <summary>A registry object behind a static readonly field: the sweep must walk into it.</summary>
    public sealed class SweepHostRegistry
    {
        private readonly Dictionary<string, List<object>> entries = new();

        public void Add(string key, object value)
        {
            if (!entries.TryGetValue(key, out var list))
                entries[key] = list = new List<object>();
            list.Add(value);
        }

        public int Count(string key) => entries.TryGetValue(key, out var list) ? list.Count : 0;

        public void Clear() => entries.Clear();
    }
}
