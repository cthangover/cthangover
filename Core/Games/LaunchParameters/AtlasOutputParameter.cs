namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Selects the output directory for a resource or localization atlas.</summary>
    public sealed class AtlasOutputParameter : StringLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public AtlasOutputParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public AtlasOutputParameter(string value) : base(value) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "atlas-out";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Selects the output directory for a resource or localization atlas.";
    }
}
