namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Keeps the launcher in the frontend even when test switches are present.</summary>
    public sealed class FrontendParameter : SwitchLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public FrontendParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public FrontendParameter(string value) : base(value) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "frontend";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Keeps the launcher in the frontend even when test switches are present.";
    }
}
