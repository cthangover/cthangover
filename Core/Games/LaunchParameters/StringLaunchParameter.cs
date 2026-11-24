namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>A parameter whose first nonempty value is used by default.</summary>
    public abstract class StringLaunchParameter : LaunchParameter<string>
    {
        /// <summary>Creates an absent argument with a null default.</summary>
        protected StringLaunchParameter() { }

        /// <summary>Parses a nonempty value while preserving its whitespace.</summary>
        protected StringLaunchParameter(string raw) : base(raw, !string.IsNullOrWhiteSpace(raw)) { }

        /// <summary>Allows a derived argument to preserve significant whitespace or accept an empty value.</summary>
        protected StringLaunchParameter(string value, bool isSpecified) : base(value, isSpecified) { }

        /// <inheritdoc />
        public override string Format => "string";
    }
}
