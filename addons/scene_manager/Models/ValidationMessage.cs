#if TOOLS
namespace SceneManagerAddon
{
    /// <summary>
    /// A single validation issue produced by
    /// <see cref="Services.SceneValidator"/>. Each message carries a
    /// human-readable description, a severity classification, and
    /// enough context (<see cref="FilePath"/> / <see cref="LineNumber"/>)
    /// for the editor to navigate the user to the offending location.
    /// </summary>
    public sealed class ValidationMessage
    {
        /// <summary>
        /// Human-readable description of the validation issue
        /// (e.g. <c>"Background 'forest' not found"</c>).
        /// </summary>
        public string Message { get; set; }

        /// <summary>
        /// How severe this issue is — errors are typically hard
        /// reference misses, warnings are softer checks like missing
        /// locale keys.
        /// </summary>
        public SeverityLevel Severity { get; set; }

        /// <summary>
        /// Relative path to the file that triggered this message
        /// (the scene JSON or scenario script that contained the
        /// offending reference).
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Optional one-based line number where the issue occurs.
        /// Currently reserved for future use; the validator does
        /// not populate it.
        /// </summary>
        public int LineNumber { get; set; }
    }
}
#endif
