namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Runs mod tests for the specified mod identity only.</summary>
    public sealed class ModTestOwnerParameter : StringLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public ModTestOwnerParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public ModTestOwnerParameter(string value) : base(value?.Trim()) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "mod";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Runs mod tests for the specified mod identity only.";
    }
}
