namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Adds comma-separated mod identities to the selected collection.</summary>
    public sealed class WithModsParameter : StringListLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public WithModsParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public WithModsParameter(string value) : base(value) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "with-mods";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Adds comma-separated mod identities to the selected collection.";
    }
}
