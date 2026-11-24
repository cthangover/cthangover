namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Blocks external debug endpoints and debug scripts for this launch.</summary>
    public sealed class NoDevelopmentControlParameter : SwitchLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public NoDevelopmentControlParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public NoDevelopmentControlParameter(string value) : base(value) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "no-development-control";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Blocks external debug endpoints and debug scripts for this launch.";
    }
}
