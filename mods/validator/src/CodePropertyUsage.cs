namespace Cthangover.Validator
{
    /// <summary>
    /// A single typed property access extracted from a mod's C# source:
    /// <c>action.GetInt("Turn", 3)</c> becomes (key=Turn, type=int, read,
    /// hasDefault=true). <c>DataBlob</c> and <c>PropertyData</c> share the same
    /// method names, so the collector intentionally does not distinguish
    /// receivers — only the key, type and direction matter for cross-mod
    /// conflict detection.
    /// </summary>
    public class CodePropertyUsage
    {
        public string ModId { get; set; }
        public string File { get; set; }
        public int Line { get; set; }
        public string Key { get; set; }
        public string Type { get; set; }
        public bool IsWrite { get; set; }
        public bool HasDefault { get; set; }
        public string Method { get; set; }
    }
}
