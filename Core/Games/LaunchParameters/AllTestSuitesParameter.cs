namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Mounts every test suite for authoring and atlas runs.</summary>
    public sealed class AllTestSuitesParameter : SwitchLaunchParameter
    {
        /// <summary>Creates a definition with the default value.</summary>
        public AllTestSuitesParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public AllTestSuitesParameter(string value) : base(value) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "with-test-suites";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Mounts every test suite for authoring and atlas runs.";
    }
}
