namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Supplies an ordered, comma-separated list of automatic dialogue choices.</summary>
    public sealed class ChoicesParameter : StringListLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public ChoicesParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public ChoicesParameter(string value) : base(value) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "choices";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Supplies an ordered, comma-separated list of automatic dialogue choices.";

        internal override bool RequestsDevelopmentMode => true;
        internal override bool ExcludesAutomaticHotReload => true;
    }
}
