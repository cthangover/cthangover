namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Overrides the core log path. A user argument takes priority over the native engine value.</summary>
    public sealed class LogFileParameter : StringLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public LogFileParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public LogFileParameter(string value) : base(value, value != null) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "log-file";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Overrides the core log path. A user argument takes priority over the native engine value.";

        internal override bool IncludeEngineArguments => true;
    }
}
