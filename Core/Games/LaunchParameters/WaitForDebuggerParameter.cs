namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Offers debugger attachment before the mod boot pipeline runs.</summary>
    public sealed class WaitForDebuggerParameter : SwitchLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public WaitForDebuggerParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public WaitForDebuggerParameter(string value) : base(value) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "wait-for-debugger";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Offers debugger attachment before the mod boot pipeline runs.";

        internal override bool RequestsDevelopmentMode => true;
        internal override bool IncludeEngineArguments => true;
    }
}
