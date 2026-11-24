namespace Cthangover.Core.Mods.Memory
{
    /// <summary>One owner-selected content preview. A preview never carries live keys or values.</summary>
    public readonly struct MemoryPreviewEntry
    {
        private const int TextLimit = 256;

        /// <summary>Optional display key or index, bounded to 256 characters.</summary>
        public string Key { get; }

        /// <summary>Optional display value, bounded to 256 characters.</summary>
        public string Value { get; }

        /// <summary>Creates a detached preview row. Owners choose which information to expose.</summary>
        public MemoryPreviewEntry(string key, string value = null)
        {
            Key = Bound(key);
            Value = Bound(value);
        }

        private static string Bound(string text)
        {
            if (text == null || text.Length <= TextLimit)
                return text;

            var length = TextLimit - 1;
            if (char.IsHighSurrogate(text[length - 1]))
                length--;
            return text[..length] + "…";
        }
    }
}
