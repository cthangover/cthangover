namespace Cthangover.Core.Games.LaunchParameters
{
    using System.Globalization;

    /// <summary>Optional duration supplied to automated scene and atlas drivers.</summary>
    public sealed class TimeoutParameter : LaunchParameter<double>
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public TimeoutParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public TimeoutParameter(string value) : this(Parse(value)) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "timeout";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Sets an automated driver's watchdog in positive seconds.";

        /// <inheritdoc />
        public override string Format => "number";

        internal override bool RequestsDevelopmentMode => true;
        internal override bool ExcludesAutomaticHotReload => true;

        private TimeoutParameter((double Value, bool Valid) parsed) : base(parsed.Value, parsed.Valid) { }

        private static (double Value, bool Valid) Parse(string raw)
        {
            var valid = double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
                && double.IsFinite(parsed) && parsed > 0;
            return (valid ? parsed : 0, valid);
        }
    }
}
