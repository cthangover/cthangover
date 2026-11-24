namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Opt-in at process startup for executing trusted C# through DebugHost.</summary>
    public sealed class AllowDebugScriptsParameter : SwitchLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public AllowDebugScriptsParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public AllowDebugScriptsParameter(string value) : base(value) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "allow-debug-scripts";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Opt-in at process startup for executing trusted C# through DebugHost.";
    }
}
