namespace Cthangover.Validator
{
    /// <summary>
    /// Capability group of a validation strategy, mapped to a user setting by
    /// <see cref="ValidatorService"/>. Strategies in a disabled feature are
    /// skipped, so expensive analysis (Roslyn, GDScript) stays opt-in without
    /// every strategy having to read the settings blob itself.
    /// </summary>
    public enum ValidationFeature
    {
        /// <summary>Cheap file-level and JSON analysis; always enabled.</summary>
        Json,

        /// <summary>Roslyn analysis of mod C# sources; gated by <c>parse_csharp</c>.</summary>
        CSharp,

        /// <summary>GDScript probe; gated by <c>parse_gdscript</c> (off by default).</summary>
        GdScript,

        /// <summary>Scenario DSL analysis; reserved for a future strategy.</summary>
        Scenarios,
    }
}
