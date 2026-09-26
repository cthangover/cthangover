namespace Cthangover.Validator
{
    /// <summary>
    /// Severity labels used by <see cref="ValidationIssue"/> and counted into
    /// <see cref="ValidationSummary"/>. Kept as string constants because the
    /// report is a machine-readable JSON contract consumed by external tooling
    /// and LLMs that expect lower-case labels.
    /// </summary>
    public static class ValidationSeverity
    {
        public const string Error = "error";
        public const string Warning = "warning";
        public const string Info = "info";
    }
}
