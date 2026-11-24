namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Activates the mod test runner after launcher boot.</summary>
    public sealed class RunTestsParameter : SwitchLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public RunTestsParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public RunTestsParameter(string value) : base(true, true) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "run-tests";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Runs mod tests after the launcher has completed boot. The legacy =value form also activates tests.";

        internal override bool RequestsDevelopmentMode => true;
        internal override bool ExcludesAutomaticHotReload => true;
    }
}
