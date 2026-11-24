namespace Cthangover.Core.Games.LaunchParameters
{
    /// <summary>Builds the mod resource and dependency catalogue and exits.</summary>
    public sealed class ResourceAtlasParameter : SwitchLaunchParameter
    {
        /// <summary>Creates a definition with the parameter's default value.</summary>
        public ResourceAtlasParameter() { }

        /// <summary>Parses the value supplied for this parameter.</summary>
        public ResourceAtlasParameter(string value) : base(value) { }

        /// <summary>Canonical argument key.</summary>
        public const string Name = "resource-atlas";

        /// <inheritdoc />
        public override string Key => Name;

        /// <inheritdoc />
        public override string Documentation => "Builds the mod resource and dependency catalogue and exits.";

        internal override bool RequestsDevelopmentMode => true;
        internal override bool ExcludesAutomaticHotReload => true;
    }
}
